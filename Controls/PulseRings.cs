using System.Numerics;
using Microsoft.UI;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using StandUP.Services;
using Windows.UI;

namespace StandUP.Controls;

/// <summary>
/// Two rounded rings that grow out from behind the mascot tile, half a period apart.
/// Vector shapes with a non-scaling stroke stay crisp at every size, and the compositor runs them.
/// </summary>
public sealed partial class PulseRings : Grid
{
    const float Period = 2.4f;
    const float Growth = 1.75f;
    // The mascot tile fills 96.5% of its frame with a corner radius of about 22% of its width.
    const float TileFill = 0.965f;
    const float CornerRatio = 0.225f;

    readonly CompositionSpriteShape[] _shapes = new CompositionSpriteShape[2];
    readonly CompositionColorBrush[] _brushes = new CompositionColorBrush[2];
    readonly CompositionRoundedRectangleGeometry[] _geometries = new CompositionRoundedRectangleGeometry[2];
    ShapeVisual? _visual;
    Color[] _colors = [Colors.White, Colors.White];
    bool _running;

    public PulseRings()
    {
        IsHitTestVisible = false;
        SizeChanged += (_, _) => Arrange();
        Unloaded += (_, _) => Stop();
    }

    public void SetColors(Color first, Color second)
    {
        _colors = [first, second];
        if (_running) Start();
    }

    public void Start()
    {
        Ensure();
        _running = true;
        var c = _visual!.Compositor;
        for (var i = 0; i < 2; i++)
        {
            var color = _colors[i];
            var bright = Color.FromArgb(150, color.R, color.G, color.B);
            var clear = Color.FromArgb(0, color.R, color.G, color.B);
            _shapes[i].StopAnimation("Scale");
            _brushes[i].StopAnimation("Color");
            _shapes[i].Scale = Vector2.One;
            _brushes[i].Color = clear;
            if (!UiMotion.AnimationsEnabled) continue;

            var delay = TimeSpan.FromSeconds(Period / 2 * i);
            var scale = c.CreateVector2KeyFrameAnimation();
            scale.InsertKeyFrame(0, Vector2.One);
            scale.InsertKeyFrame(1, new Vector2(Growth), c.CreateCubicBezierEasingFunction(new Vector2(0.2f, 0.6f), new Vector2(0.35f, 1f)));
            scale.Duration = TimeSpan.FromSeconds(Period);
            scale.IterationBehavior = AnimationIterationBehavior.Forever;
            scale.DelayTime = delay;

            var fade = c.CreateColorKeyFrameAnimation();
            fade.InterpolationColorSpace = CompositionColorSpace.Rgb;
            fade.InsertKeyFrame(0, bright);
            fade.InsertKeyFrame(0.15f, bright);
            fade.InsertKeyFrame(1, clear, c.CreateCubicBezierEasingFunction(new Vector2(0.3f, 0f), new Vector2(0.6f, 1f)));
            fade.Duration = scale.Duration;
            fade.IterationBehavior = AnimationIterationBehavior.Forever;
            fade.DelayTime = delay;

            _shapes[i].StartAnimation("Scale", scale);
            _brushes[i].StartAnimation("Color", fade);
        }
    }

    public void Stop()
    {
        _running = false;
        if (_visual is null) return;
        for (var i = 0; i < 2; i++)
        {
            _shapes[i].StopAnimation("Scale");
            _brushes[i].StopAnimation("Color");
            _brushes[i].Color = Colors.Transparent;
        }
    }

    void Ensure()
    {
        if (_visual is not null) return;
        var c = ElementCompositionPreview.GetElementVisual(this).Compositor;
        _visual = c.CreateShapeVisual();
        for (var i = 0; i < 2; i++)
        {
            _geometries[i] = c.CreateRoundedRectangleGeometry();
            _brushes[i] = c.CreateColorBrush(Colors.Transparent);
            _shapes[i] = c.CreateSpriteShape(_geometries[i]);
            _shapes[i].StrokeBrush = _brushes[i];
            _shapes[i].StrokeThickness = 2;
            _shapes[i].IsStrokeNonScaling = true;
            _visual.Shapes.Add(_shapes[i]);
        }
        ElementCompositionPreview.SetElementChildVisual(this, _visual);
        Arrange();
    }

    void Arrange()
    {
        if (_visual is null) return;
        var size = new Vector2((float)ActualWidth, (float)ActualHeight);
        // Leave room around the stage so grown rings are never cut at the visual's bounds.
        var margin = size * Growth;
        _visual.Offset = new Vector3(-margin, 0);
        _visual.Size = size + margin * 2;
        var side = MathF.Min(size.X, size.Y) * TileFill;
        var offset = margin + (size - new Vector2(side)) / 2;
        for (var i = 0; i < 2; i++)
        {
            _geometries[i].Size = new Vector2(side);
            _geometries[i].Offset = offset;
            _geometries[i].CornerRadius = new Vector2(side * CornerRatio);
            _shapes[i].CenterPoint = margin + size / 2;
        }
    }
}
