using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Lab2.RegexLang;

namespace Lab2;

/// <summary>
/// Match highlighting inside an editable TextBox: draws soft translucent green rectangles
/// underneath the text, positioned through the TextBox's own TextLayout. Coordinates are
/// recomputed on every frame, so the highlight follows scrolling, text edits and window
/// resizes.
/// </summary>
public sealed class HighlightOverlay : Control
{
    private static readonly IBrush HighlightBrush =
        new SolidColorBrush(Color.FromArgb(0x80, 0x6E, 0xC7, 0x86));

    private readonly DispatcherTimer _timer;
    private TextBox? _target;
    private IReadOnlyList<MatchSpan>? _ranges;

    public HighlightOverlay()
    {
        IsHitTestVisible = false;
        ClipToBounds = true;

        // Scrolling the TextBox does not change this control's properties, so while there
        // is anything to highlight we refresh on a timer.
        _timer = new DispatcherTimer(
            TimeSpan.FromMilliseconds(50),
            DispatcherPriority.Background,
            OnTimerTick);
        _timer.Start();
    }

    /// <summary>The TextBox whose matches are highlighted.</summary>
    public TextBox? Target
    {
        get => _target;
        set
        {
            _target = value;
            InvalidateVisual();
        }
    }

    /// <summary>Match ranges (character indices in the TextBox text).</summary>
    public IReadOnlyList<MatchSpan>? Ranges
    {
        get => _ranges;
        set
        {
            _ranges = value;
            InvalidateVisual();
        }
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        if (_ranges is { Count: > 0 })
            InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        if (_target is not { } target || _ranges is not { Count: > 0 })
            return;

        var presenter = target.GetVisualDescendants().OfType<TextPresenter>().FirstOrDefault();
        if (presenter is null)
            return;

        // Origin of the text coordinates within this control.
        var origin = presenter.TranslatePoint(new Point(0, 0), this);
        if (origin is null)
            return;

        var layout = presenter.TextLayout;
        if (layout is null)
            return;

        foreach (var span in _ranges)
        {
            if (span.Length <= 0)
                continue;

            foreach (var rect in layout.HitTestTextRange(span.Start, span.Length))
            {
                context.FillRectangle(
                    HighlightBrush,
                    new Rect(
                        origin.Value.X + rect.X,
                        origin.Value.Y + rect.Y,
                        rect.Width,
                        rect.Height));
            }
        }
    }
}
