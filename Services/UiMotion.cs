using System.Numerics;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Hosting;
using Windows.UI.ViewManagement;

namespace StandUP.Services;

/// <summary>Small, optional UI effects that follow the Windows animation preference.</summary>
/// <remarks>Everything runs on the compositor thread, so a busy UI thread cannot make it stutter.</remarks>
public static class UiMotion
{
    static readonly UISettings Settings = new();
    public static event Action? AnimationsChanged;
    static UiMotion() => Settings.AnimationsEnabledChanged += (_, _) => AnimationsChanged?.Invoke();
    public static bool AnimationsEnabled => Settings.AnimationsEnabled;

    // Fluent motion curves: decelerate for things arriving, accelerate for things leaving.
    public static CompositionEasingFunction Decelerate(Compositor c) =>
        c.CreateCubicBezierEasingFunction(new Vector2(0.1f, 0.9f), new Vector2(0.2f, 1f));

    public static CompositionEasingFunction Accelerate(Compositor c) =>
        c.CreateCubicBezierEasingFunction(new Vector2(0.7f, 0f), new Vector2(1f, 0.5f));

    public static CompositionEasingFunction Standard(Compositor c) =>
        c.CreateCubicBezierEasingFunction(new Vector2(0.8f, 0f), new Vector2(0.2f, 1f));

    /// <summary>Fades an element in while it settles upward by <paramref name="distance"/>.</summary>
    public static void Reveal(UIElement element, float distance = 12, int milliseconds = 333)
    {
        var visual = ElementCompositionPreview.GetElementVisual(element);
        ElementCompositionPreview.SetIsTranslationEnabled(element, true);
        visual.StopAnimation("Opacity");
        visual.StopAnimation("Translation.Y");
        if (!AnimationsEnabled)
        {
            visual.Opacity = 1;
            visual.Properties.InsertVector3("Translation", Vector3.Zero);
            return;
        }
        var c = visual.Compositor;
        var fade = c.CreateScalarKeyFrameAnimation();
        fade.InsertKeyFrame(0, 0);
        fade.InsertKeyFrame(1, 1, c.CreateLinearEasingFunction());
        fade.Duration = TimeSpan.FromMilliseconds(milliseconds * 0.6);
        var slide = c.CreateScalarKeyFrameAnimation();
        slide.InsertKeyFrame(0, distance);
        slide.InsertKeyFrame(1, 0, Decelerate(c));
        slide.Duration = TimeSpan.FromMilliseconds(milliseconds);
        visual.StartAnimation("Opacity", fade);
        visual.StartAnimation("Translation.Y", slide);
    }

    /// <summary>Fades an element out while it sinks by <paramref name="distance"/>, then runs <paramref name="completed"/>.</summary>
    public static void Conceal(UIElement element, float distance, Action completed, int milliseconds = 167)
    {
        var visual = ElementCompositionPreview.GetElementVisual(element);
        ElementCompositionPreview.SetIsTranslationEnabled(element, true);
        if (!AnimationsEnabled)
        {
            completed();
            visual.Opacity = 1;
            return;
        }
        var c = visual.Compositor;
        var fade = c.CreateScalarKeyFrameAnimation();
        fade.InsertKeyFrame(1, 0, Accelerate(c));
        fade.Duration = TimeSpan.FromMilliseconds(milliseconds);
        var slide = c.CreateScalarKeyFrameAnimation();
        slide.InsertKeyFrame(1, distance, Accelerate(c));
        slide.Duration = fade.Duration;
        var batch = c.CreateScopedBatch(CompositionBatchTypes.Animation);
        visual.StartAnimation("Opacity", fade);
        visual.StartAnimation("Translation.Y", slide);
        batch.End();
        var queue = element.DispatcherQueue;
        // The element keeps its faded end state; the next Reveal starts from scratch.
        batch.Completed += (_, _) => queue.TryEnqueue(() => completed());
    }

    /// <summary>A short, soft pop for small badges and icons.</summary>
    public static void Emphasize(FrameworkElement element)
    {
        if (!AnimationsEnabled) return;
        var visual = ElementCompositionPreview.GetElementVisual(element);
        visual.CenterPoint = new Vector3((float)element.ActualWidth / 2, (float)element.ActualHeight / 2, 0);
        var c = visual.Compositor;
        var scale = c.CreateVector3KeyFrameAnimation();
        scale.InsertKeyFrame(0.35f, new Vector3(1.08f, 1.08f, 1), Decelerate(c));
        scale.InsertKeyFrame(1, Vector3.One, Standard(c));
        scale.Duration = TimeSpan.FromMilliseconds(420);
        visual.StartAnimation("Scale", scale);
    }
}
