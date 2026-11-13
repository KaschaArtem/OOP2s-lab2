using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Lab2.RegexLang;

namespace Lab2;

public partial class MainWindow : Window
{
    // Soft status palette: all backgrounds share the same lightness, text tones are muted.
    private static readonly IBrush NeutralBackground = new SolidColorBrush(Color.Parse("#EFF1F4"));
    private static readonly IBrush NeutralForeground = new SolidColorBrush(Color.Parse("#55606E"));
    private static readonly IBrush SuccessBackground = new SolidColorBrush(Color.Parse("#E5F2E9"));
    private static readonly IBrush SuccessForeground = new SolidColorBrush(Color.Parse("#3F7B55"));
    private static readonly IBrush WarningBackground = new SolidColorBrush(Color.Parse("#FBF1DF"));
    private static readonly IBrush WarningForeground = new SolidColorBrush(Color.Parse("#8B6B31"));
    private static readonly IBrush ErrorBackground = new SolidColorBrush(Color.Parse("#F9EAEA"));
    private static readonly IBrush ErrorForeground = new SolidColorBrush(Color.Parse("#A45A5A"));

    private ReferenceWindow? _referenceWindow;

    public MainWindow()
    {
        InitializeComponent();

        Overlay.Target = InputTextBox;

        // Any edit makes previously found ranges stale.
        PatternBox.TextChanged += (_, _) => ResetResult();
        InputTextBox.TextChanged += (_, _) => ResetResult();
    }

    private void ResetResult()
    {
        Overlay.Ranges = null;
        SetStatus(
            "Enter an expression and a string, then click \"Check\".",
            NeutralBackground,
            NeutralForeground);
    }

    private void OnCheckClick(object? sender, RoutedEventArgs e) => RunCheck();

    private void OnReferenceClick(object? sender, RoutedEventArgs e)
    {
        // The reference is non-modal: the main window stays usable while it is open.
        // Pressing "?" again just brings the existing window to the front.
        if (_referenceWindow is { } reference)
        {
            if (reference.WindowState == WindowState.Minimized)
                reference.WindowState = WindowState.Normal;

            reference.Activate();
            return;
        }

        reference = new ReferenceWindow();
        reference.Closed += (_, _) => _referenceWindow = null;
        _referenceWindow = reference;
        reference.Show(this);
    }

    private void RunCheck()
    {
        string pattern = PatternBox.Text ?? string.Empty;
        string text = InputTextBox.Text ?? string.Empty;

        if (pattern.Length == 0)
        {
            Overlay.Ranges = null;
            SetStatus(
                "Warning: enter an expression to check.",
                WarningBackground,
                WarningForeground);
            return;
        }

        if (text.Length == 0)
        {
            Overlay.Ranges = null;
            SetStatus(
                "Warning: enter a string to search in.",
                WarningBackground,
                WarningForeground);
            return;
        }

        AstNode expression;
        try
        {
            expression = RegexpParser.Parse(pattern);
        }
        catch (RegexSyntaxException ex)
        {
            Overlay.Ranges = null;
            SetStatus(
                $"The expression is not regular: {ex.Message} (position {ex.Position + 1}).",
                ErrorBackground,
                ErrorForeground);
            return;
        }

        IReadOnlyList<MatchSpan> matches;
        try
        {
            matches = RegexpMatcher.FindAll(text, expression);
        }
        catch (MatchComplexityException ex)
        {
            Overlay.Ranges = null;
            SetStatus(ex.Message, WarningBackground, WarningForeground);
            return;
        }

        if (matches.Count == 0)
        {
            Overlay.Ranges = null;
            SetStatus(
                "The expression is regular, but it was not found in the string.",
                WarningBackground,
                WarningForeground);
            return;
        }

        Overlay.Ranges = matches;
        SetStatus(
            $"The expression is regular. Matches found: {matches.Count}.",
            SuccessBackground,
            SuccessForeground);
    }

    private void SetStatus(string message, IBrush background, IBrush foreground)
    {
        StatusText.Text = message;
        StatusBorder.Background = background;
        StatusText.Foreground = foreground;
    }
}
