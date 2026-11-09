using System.Diagnostics;
using System.Numerics;
using Microsoft.UI.Composition;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using StandUP.Services;
using Windows.Foundation;
using Windows.Storage;

namespace StandUP.Controls;

public enum MascotState { Sitting, Standing, Prompt, Paused, Celebrating, SitPrompt }

/// <summary>
/// The original clay artwork, played by the native compositor.
/// </summary>
/// <remarks>
/// <para>The sit/stand clip is a 16 x 16 atlas of 256 px frames, decoded at the size it is drawn so the GPU samples
/// each frame close to 1:1.</para>
/// <para>The wave is a cut-out rig made from the original wave frame: the resting body with the right arm removed,
/// plus that arm as its own layer. The arm really rotates at the shoulder, so the hand travels instead of
/// cross-fading between poses.</para>
/// </remarks>
public sealed partial class StandupMascot : Button
{
    const int FrameSize = 256;
    const int Columns = 16;
    const int LastFrame = 240;
    const double FramesPerSecond = 60;
    const int MinFramePixels = 64;
    const int FramePixelStep = 32;
    // Pose changes are eased in time and run a little slower than the raw 60 fps clip.
    const double PoseTimeScale = 1.25;
    // Shoulder joint of the wave arm, in frame pixels; the arm image rests at 0 degrees with the hand raised.
    static readonly Vector2 Shoulder = new(151, 139);
    const float ArmOnLap = 100;

    // The clip was interpolated between key poses. Where the arms change place it cross-faded instead of moving
    // them, leaving translucent "ghost" arms. Playback skips those frames and covers the cut with a small pop.
    static readonly (int From, int To)[] Ghosts = [(68, 74), (167, 172)];

    static readonly Dictionary<string, Task<LoadedImageSurface?>> SharedSurfaces = [];

    readonly Grid _scene = new() { Width = FrameSize, Height = FrameSize };
    readonly Grid _spriteHost = new() { Width = FrameSize, Height = FrameSize };
    readonly Image _fallback = new()
    {
        Width = FrameSize, Height = FrameSize, Stretch = Stretch.Uniform,
        Source = new BitmapImage(new Uri(Path.Combine(AppContext.BaseDirectory, "Assets", "Mascot", "frame-01.png")))
        { DecodePixelWidth = FrameSize, DecodePixelType = DecodePixelType.Logical },
    };
    readonly Stopwatch _clipClock = new();
    readonly DispatcherQueueTimer _idleTimer;
    ContainerVisual? _rig;
    SpriteVisual? _sprite;
    SpriteVisual? _waveBody;
    SpriteVisual? _waveArm;
    CompositionSurfaceBrush? _brush;
    XamlRoot? _root;
    MascotState _state;
    MotionClip? _clip;
    bool _loaded;
    bool _ready;
    bool _active;
    bool _waving;
    int _frame;
    int _clipGeneration;
    int _framePixels;
    int _loadingPixels;
    float _unit = FrameSize;

    public StandupMascot()
    {
        Style = (Style)Application.Current.Resources["MascotButtonStyle"];
        AutomationProperties.SetName(this, Loc.T("mascot.action"));
        _scene.Children.Add(_fallback);
        _scene.Children.Add(_spriteHost);
        Content = new Viewbox { Stretch = Stretch.Uniform, Child = _scene };
        _idleTimer = DispatcherQueue.CreateTimer();
        _idleTimer.IsRepeating = false;
        _idleTimer.Tick += (_, _) => Wave();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        Click += (_, _) => ReactToTap();
    }

    public MascotState State
    {
        get => _state;
        set
        {
            if (_state == value) return;
            var current = CurrentFrame;
            _state = value;
            StopWave();
            _idleTimer.Stop();
            if (value == MascotState.SitPrompt) current = 180;
            FreezeFrame(!_ready ? InitialFrame(value) : current);
            UpdateState();
        }
    }

    static int InitialFrame(MascotState state) => state switch
    {
        MascotState.SitPrompt => 180,
        MascotState.Standing => 60,
        MascotState.Celebrating => 120,
        _ => 0,
    };

    static int RestFrame(MascotState state) => state switch
    {
        MascotState.Prompt or MascotState.Celebrating => 120,
        MascotState.Standing => 60,
        MascotState.SitPrompt => 240,
        _ => 0,
    };

    bool CanAnimate => _loaded && _ready && XamlRoot?.IsHostVisible == true && UiMotion.AnimationsEnabled;

    int CurrentFrame
    {
        get
        {
            if (!_clipClock.IsRunning || _clip is null) return _frame;
            var elapsed = _clipClock.Elapsed.TotalSeconds;
            var frame = _clip.Keys[0].Frame;
            foreach (var (time, key) in _clip.Keys)
            {
                if (time > elapsed) break;
                frame = key;
            }
            return frame;
        }
    }

    // Sine ease-in-out: the source frames move at constant speed, which reads as mechanical.
    static double Ease(double t) => (1 - Math.Cos(Math.PI * t)) / 2;

    // ---------------------------------------------------------------- lifecycle

    async void OnLoaded(object sender, RoutedEventArgs e)
    {
        _loaded = true;
        _root = XamlRoot;
        if (_root is not null) _root.Changed += OnRootChanged;
        SizeChanged += OnSizeChanged;
        UiMotion.AnimationsChanged += OnAnimationsChanged;
        await EnsureAtlasAsync();
    }

    void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_root is not null) _root.Changed -= OnRootChanged;
        _root = null;
        SizeChanged -= OnSizeChanged;
        UiMotion.AnimationsChanged -= OnAnimationsChanged;
        _loaded = false;
        _idleTimer.Stop();
        StopWave();
        FreezeFrame(CurrentFrame);
        _active = false;
    }

    async void OnSizeChanged(object sender, SizeChangedEventArgs e) => await EnsureAtlasAsync();

    async void OnRootChanged(XamlRoot sender, XamlRootChangedEventArgs args)
    {
        var active = CanAnimate;
        if (active != _active)
        {
            _active = active;
            if (active) UpdateState();
            else { _idleTimer.Stop(); StopWave(); FreezeFrame(CurrentFrame); }
        }
        // Moving to a display with a higher scale factor needs sharper images.
        await EnsureAtlasAsync();
    }

    void OnAnimationsChanged() => DispatcherQueue.TryEnqueue(UpdateState);

    // ---------------------------------------------------------------- images

    /// <summary>Physical pixels one frame covers on screen, rounded up to a shared step.</summary>
    int WantedFramePixels()
    {
        var side = Math.Min(ActualWidth, ActualHeight);
        if (side <= 0 || XamlRoot is null) return 0;
        var pixels = (int)Math.Ceiling(side * XamlRoot.RasterizationScale / FramePixelStep) * FramePixelStep;
        return Math.Clamp(pixels, MinFramePixels, FrameSize);
    }

    static Task<LoadedImageSurface?> Shared(string name, int framePixels, int columns)
    {
        var key = $"{name}@{framePixels}";
        if (!SharedSurfaces.TryGetValue(key, out var task))
        {
            SharedSurfaces[key] = task = LoadAsync(name, framePixels * columns, key);
        }
        return task;
    }

    static async Task<LoadedImageSurface?> LoadAsync(string name, int side, string key)
    {
        try
        {
            var file = await StorageFile.GetFileFromPathAsync(Path.Combine(AppContext.BaseDirectory, "Assets", "Mascot", name));
            using var stream = await file.OpenReadAsync();
            // The decoder downsamples with a high-quality filter, which the GPU cannot do per frame.
            var surface = LoadedImageSurface.StartLoadFromStream(stream, new Size(side, side));
            var completed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            surface.LoadCompleted += (_, args) => completed.TrySetResult(args.Status == LoadedImageSourceLoadStatus.Success);
            if (await completed.Task) return surface;
            App.LogCrash(new InvalidOperationException($"Could not load mascot image: {name}"));
            surface.Dispose();
        }
        catch (Exception ex)
        {
            App.LogCrash(ex);
        }
        SharedSurfaces.Remove(key);
        return null;
    }

    async Task EnsureAtlasAsync()
    {
        var wanted = WantedFramePixels();
        // Keep sharper images when the mascot shrinks; only load when more detail is needed.
        if (wanted == 0 || wanted <= _framePixels || wanted <= _loadingPixels) return;
        _loadingPixels = wanted;
        try
        {
            var atlas = Shared("standup-atlas.png", wanted, Columns);
            var body = Shared("wave-body.png", wanted, 1);
            var arm = Shared("wave-arm.png", wanted, 1);
            var surface = await atlas;
            var bodySurface = await body;
            var armSurface = await arm;
            if (surface is null || !_loaded || _loadingPixels != wanted) return;
            Apply(surface, bodySurface, armSurface, wanted);
        }
        catch (Exception ex)
        {
            App.LogCrash(ex);
        }
        finally
        {
            if (_loadingPixels == wanted) _loadingPixels = 0;
        }
    }

    void Apply(LoadedImageSurface atlas, LoadedImageSurface? body, LoadedImageSurface? arm, int framePixels)
    {
        var frame = _ready ? CurrentFrame : _frame;
        StopWave();
        _framePixels = framePixels;
        // Frame size in surface units; the rig is scaled back to the 256-unit scene.
        _unit = (float)atlas.DecodedSize.Width / Columns;
        var c = ElementCompositionPreview.GetElementVisual(_spriteHost).Compositor;
        if (_rig is null)
        {
            _rig = c.CreateContainerVisual();
            _brush = c.CreateSurfaceBrush(atlas);
            _brush.Stretch = CompositionStretch.None;
            _brush.HorizontalAlignmentRatio = 0;
            _brush.VerticalAlignmentRatio = 0;
            _waveBody = c.CreateSpriteVisual();
            _sprite = c.CreateSpriteVisual();
            _sprite.Brush = _brush;
            _sprite.Clip = c.CreateInsetClip();
            _waveArm = c.CreateSpriteVisual();
            // Back to front: wave body, the frame on top of it (faded out while waving), the moving arm.
            _rig.Children.InsertAtTop(_waveBody);
            _rig.Children.InsertAtTop(_sprite);
            _rig.Children.InsertAtTop(_waveArm);
            ElementCompositionPreview.SetElementChildVisual(_spriteHost, _rig);
        }
        else _brush!.Surface = atlas;
        _rig.Scale = new Vector3(FrameSize / _unit, FrameSize / _unit, 1);
        _sprite!.Size = new Vector2(_unit);
        foreach (var (visual, surface) in new[] { (_waveBody!, body), (_waveArm!, arm) })
        {
            visual.Size = new Vector2(_unit);
            visual.Brush = surface is null ? null : c.CreateSurfaceBrush(surface);
            visual.IsVisible = false;
        }
        _waveArm!.CenterPoint = new Vector3(Shoulder * (_unit / FrameSize), 0);
        _ready = true;
        FreezeFrame(frame);
        // No crossfade: retain the real first frame until the complete texture is ready.
        _fallback.Visibility = Visibility.Collapsed;
        UpdateState();
    }

    Vector2 AtlasOffset(int frame) => new(-(frame % Columns) * _unit, -(frame / Columns) * _unit);

    // ---------------------------------------------------------------- frame clips

    static int Deghost(int frame, int direction)
    {
        foreach (var (a, b) in Ghosts)
        {
            if (frame < a || frame > b) continue;
            var mid = (a + b) / 2.0;
            return direction > 0 ? (frame < mid ? a - 1 : b + 1)
                : direction < 0 ? (frame > mid ? b + 1 : a - 1)
                : (frame < mid ? a - 1 : b + 1);
        }
        return frame;
    }

    static bool Crosses(int previous, int next) =>
        Ghosts.Any(g => (previous < g.From && next > g.To) || (previous > g.To && next < g.From));

    /// <summary>A sequence of source frames with the moment each one appears.</summary>
    sealed class MotionClip
    {
        public readonly List<(double Time, int Frame)> Keys = [];
        public readonly List<double> Pops = [];
        public double Seconds;
        public int Last => Keys[^1].Frame;
    }

    static void Append(MotionClip clip, int from, int to, double seconds)
    {
        var start = clip.Seconds;
        var count = Math.Abs(to - from);
        var direction = Math.Sign(to - from);
        // One key per display frame at 60 Hz; easing picks which source frame shows at each moment.
        var samples = Math.Max(1, (int)Math.Round(seconds * FramesPerSecond));
        int? previous = clip.Keys.Count > 0 ? clip.Last : null;
        for (var i = clip.Keys.Count == 0 ? 0 : 1; i <= samples; i++)
        {
            var t = (double)i / samples;
            var frame = Deghost(from + direction * (int)Math.Round(Ease(t) * count), direction);
            if (previous == frame) continue;
            var time = start + t * seconds;
            if (previous is int p && Crosses(p, frame)) clip.Pops.Add(time);
            clip.Keys.Add((time, frame));
            previous = frame;
        }
        clip.Seconds = start + seconds;
    }

    void FreezeFrame(int frame)
    {
        _clipGeneration++;
        _clipClock.Reset();
        _clip = null;
        _frame = Deghost(Math.Clamp(frame, 0, LastFrame), 0);
        if (_brush is not null)
        {
            _brush.StopAnimation("Offset");
            _brush.Offset = AtlasOffset(_frame);
        }
        var host = ElementCompositionPreview.GetElementVisual(_spriteHost);
        host.StopAnimation("Scale");
        host.Scale = Vector3.One;
    }

    void UpdateState()
    {
        _active = CanAnimate;
        _idleTimer.Stop();
        if (!_ready) return;
        if (!CanAnimate)
        {
            StopWave();
            FreezeFrame(UiMotion.AnimationsEnabled ? CurrentFrame : RestFrame(_state));
            return;
        }
        if (_state == MascotState.Paused)
        {
            FreezeFrame(CurrentFrame);
            return;
        }
        if (_waving) return;
        var current = CurrentFrame;
        switch (_state)
        {
            case MascotState.Prompt:
            case MascotState.Celebrating:
                PlayFrames(current > 180 ? 0 : current, 120);
                break;
            case MascotState.SitPrompt:
                PlayFrames(current < 160 ? 180 : current, 240);
                break;
            case MascotState.Standing:
                PlayFrames(current > 200 ? 0 : current, current >= 100 ? 180 : 60);
                break;
            default:
                if (current > 0 && current < 200) PlayFrames(current, 0, ScheduleIdle);
                else { FreezeFrame(0); ScheduleIdle(); }
                break;
        }
    }

    void PlayFrames(int from, int to, Action? completed = null)
    {
        FreezeFrame(from);
        from = _frame;
        if (!CanAnimate || from == to || _brush is null) { FreezeFrame(to); completed?.Invoke(); return; }
        var clip = new MotionClip();
        Append(clip, from, to, Math.Abs(to - from) / FramesPerSecond * PoseTimeScale);
        if (clip.Keys.Count < 2) { FreezeFrame(to); completed?.Invoke(); return; }

        var compositor = _brush.Compositor;
        var animation = compositor.CreateVector2KeyFrameAnimation();
        var step = compositor.CreateStepEasingFunction(1);
        step.IsInitialStepSingleFrame = false;
        step.IsFinalStepSingleFrame = true;
        foreach (var (time, frame) in clip.Keys)
            animation.InsertKeyFrame((float)(time / clip.Seconds), AtlasOffset(frame), step);
        animation.Duration = TimeSpan.FromSeconds(clip.Seconds);

        var generation = _clipGeneration;
        var last = clip.Last;
        _clip = clip;
        _clipClock.Restart();
        var batch = compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
        batch.Completed += (_, _) => DispatcherQueue.TryEnqueue(() =>
        {
            if (generation != _clipGeneration) return;
            FreezeFrame(last);
            if (CanAnimate) completed?.Invoke();
        });
        _brush.StartAnimation("Offset", animation);
        StartPops(clip);
        batch.End();
    }

    void StartPops(MotionClip clip)
    {
        if (clip.Pops.Count == 0) return;
        var host = ElementCompositionPreview.GetElementVisual(_spriteHost);
        var c = host.Compositor;
        host.CenterPoint = new Vector3(FrameSize / 2f, FrameSize / 2f, 0);
        var seconds = Math.Max(clip.Seconds, clip.Pops[^1] + 0.2);
        var pop = c.CreateVector3KeyFrameAnimation();
        var rise = c.CreateCubicBezierEasingFunction(new Vector2(0.2f, 0f), new Vector2(0.4f, 1f));
        var settle = c.CreateCubicBezierEasingFunction(new Vector2(0.3f, 0f), new Vector2(0.2f, 1f));
        var peak = new Vector3(1.04f, 1.04f, 1);
        pop.InsertKeyFrame(0, Vector3.One);
        var placed = 0f;
        foreach (var time in clip.Pops)
        {
            // Swell just before the cut and settle after it, so the pose change lands inside the bounce.
            var keys = new[] { (time - 0.07, Vector3.One, rise), (time + 0.02, peak, rise), (time + 0.2, Vector3.One, settle) };
            foreach (var (at, value, easing) in keys)
            {
                var progress = (float)Math.Clamp(at / seconds, 0, 1);
                if (progress <= placed) continue;
                pop.InsertKeyFrame(progress, value, easing);
                placed = progress;
            }
        }
        if (placed < 1) pop.InsertKeyFrame(1, Vector3.One);
        pop.Duration = TimeSpan.FromSeconds(seconds);
        host.StartAnimation("Scale", pop);
    }

    // ---------------------------------------------------------------- wave

    void ScheduleIdle()
    {
        _idleTimer.Stop();
        if (!CanAnimate || _state != MascotState.Sitting || _clipClock.IsRunning) return;
        _idleTimer.Interval = TimeSpan.FromSeconds(25 + Random.Shared.NextDouble() * 20);
        _idleTimer.Start();
    }

    /// <summary>Lifts the right hand off the lap, waves from the shoulder, and puts it back.</summary>
    void Wave()
    {
        if (!CanAnimate || _waving || _state != MascotState.Sitting || _clipClock.IsRunning || _frame != 0
            || _waveBody?.Brush is null || _waveArm?.Brush is null || _sprite is null)
        {
            ScheduleIdle();
            return;
        }
        _idleTimer.Stop();
        _waving = true;
        var c = _sprite.Compositor;
        var lift = c.CreateCubicBezierEasingFunction(new Vector2(0.2f, 0.7f), new Vector2(0.3f, 1f));
        var swing = c.CreateCubicBezierEasingFunction(new Vector2(0.45f, 0f), new Vector2(0.55f, 1f));
        var drop = c.CreateCubicBezierEasingFunction(new Vector2(0.5f, 0f), new Vector2(0.8f, 0.4f));
        var linear = c.CreateLinearEasingFunction();

        // Timeline in seconds: lift 0-0.38, swing 0.38-1.3, lower 1.3-1.64.
        const double total = 1.64;
        float At(double seconds) => (float)(seconds / total);
        var arm = c.CreateScalarKeyFrameAnimation();
        arm.InsertKeyFrame(0, ArmOnLap);
        arm.InsertKeyFrame(At(0.38), 0, lift);
        arm.InsertKeyFrame(At(0.58), -12, swing);
        arm.InsertKeyFrame(At(0.8), 14, swing);
        arm.InsertKeyFrame(At(1.02), -12, swing);
        arm.InsertKeyFrame(At(1.2), 6, swing);
        arm.InsertKeyFrame(At(1.3), 0, swing);
        arm.InsertKeyFrame(1, ArmOnLap, drop);
        arm.Duration = TimeSpan.FromSeconds(total);

        // The resting frame dissolves into the arm-less body while the solid arm is already moving.
        var frame = c.CreateScalarKeyFrameAnimation();
        frame.InsertKeyFrame(0, 1);
        frame.InsertKeyFrame(At(0.14), 0, linear);
        frame.InsertKeyFrame(At(1.5), 0, linear);
        frame.InsertKeyFrame(1, 1, linear);
        frame.Duration = arm.Duration;

        _waveBody.IsVisible = true;
        _waveArm.IsVisible = true;
        _waveArm.RotationAngleInDegrees = ArmOnLap;
        var generation = ++_clipGeneration;
        var batch = c.CreateScopedBatch(CompositionBatchTypes.Animation);
        _waveArm.StartAnimation("RotationAngleInDegrees", arm);
        _sprite.StartAnimation("Opacity", frame);
        batch.End();
        batch.Completed += (_, _) => DispatcherQueue.TryEnqueue(() =>
        {
            if (generation != _clipGeneration) return;
            StopWave();
            ScheduleIdle();
        });
    }

    void StopWave()
    {
        if (!_waving) return;
        _waving = false;
        _sprite?.StopAnimation("Opacity");
        if (_sprite is not null) _sprite.Opacity = 1;
        _waveArm?.StopAnimation("RotationAngleInDegrees");
        if (_waveArm is not null) _waveArm.IsVisible = false;
        if (_waveBody is not null) _waveBody.IsVisible = false;
    }

    void ReactToTap()
    {
        if (!CanAnimate || _state != MascotState.Sitting) return;
        Wave();
    }
}
