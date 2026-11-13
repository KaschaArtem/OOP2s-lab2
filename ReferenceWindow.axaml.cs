using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Lab2;

/// <summary>
/// Cheat sheet for the custom regular language. Every symbol and construct gets its own
/// row: a monospace "chip" with the token on the left and a short description on the right.
/// </summary>
public partial class ReferenceWindow : Window
{
    private static readonly IBrush ChipBackground = new SolidColorBrush(Color.Parse("#EFF1F4"));
    private static readonly IBrush ChipForeground = new SolidColorBrush(Color.Parse("#33404F"));
    private static readonly IBrush SectionForeground = new SolidColorBrush(Color.Parse("#445060"));
    private static readonly IBrush TextForeground = new SolidColorBrush(Color.Parse("#55606E"));

    private static readonly FontFamily Mono = new("Consolas,monospace");

    public ReferenceWindow()
    {
        InitializeComponent();

        foreach (var (title, rows) in Sections())
        {
            SectionsPanel.Children.Add(new TextBlock
            {
                Text = title,
                FontWeight = FontWeight.SemiBold,
                FontSize = 13,
                Foreground = SectionForeground,
                Margin = new Thickness(0, 10, 0, 3),
            });

            foreach (var (token, description) in rows)
                SectionsPanel.Children.Add(CreateRow(token, description));
        }
    }

    private static Control CreateRow(string token, string description)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("190,*"),
        };

        var chip = new Border
        {
            Background = ChipBackground,
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(9, 3),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = token,
                FontFamily = Mono,
                FontSize = 12.5,
                Foreground = ChipForeground,
            },
        };
        Grid.SetColumn(chip, 0);

        var text = new TextBlock
        {
            Text = description,
            FontSize = 13,
            Foreground = TextForeground,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(text, 1);

        grid.Children.Add(chip);
        grid.Children.Add(text);
        return grid;
    }

    private static (string Title, (string Token, string Description)[] Rows)[] Sections() => new[]
    {
        ("Literals", new (string, string)[]
        {
            ("abc", "the text abc"),
            (".", "any character except \\n"),
        }),

        ("Escapes", new (string, string)[]
        {
            ("\\n", "line break"),
            ("\\r", "carriage return"),
            ("\\t", "tab"),
            ("\\f", "form feed"),
            ("\\v", "vertical tab"),
            ("\\\\", "backslash"),
            ("\\.", "a literal dot"),
            ("\\*", "a literal *"),
            ("\\+", "a literal +"),
            ("\\?", "a literal ?"),
            ("\\(", "a literal ("),
            ("\\)", "a literal )"),
            ("\\[", "a literal ["),
            ("\\]", "a literal ]"),
            ("\\{", "a literal {"),
            ("\\}", "a literal }"),
            ("\\|", "a literal |"),
            ("\\^", "a literal ^"),
            ("\\$", "a literal $"),
        }),

        ("Character classes", new (string, string)[]
        {
            ("[abc]", "any of these characters"),
            ("[a-z]", "any character in the range"),
            ("[^0-9]", "any character except these"),
            ("[]]", "] in first position is a literal"),
        }),

        ("Predefined classes", new (string, string)[]
        {
            ("\\d", "a digit 0-9"),
            ("\\D", "not a digit"),
            ("\\w", "a letter, digit or _"),
            ("\\W", "not a word character"),
            ("\\s", "a whitespace character"),
            ("\\S", "not whitespace"),
        }),

        ("Quantifiers", new (string, string)[]
        {
            ("*", "0 or more, greedy"),
            ("+", "1 or more, greedy"),
            ("?", "optional: 0 or 1"),
            ("{m}", "exactly m times"),
            ("{m,}", "m times or more"),
            ("{m,n}", "m to n times"),
        }),

        ("Grouping and alternation", new (string, string)[]
        {
            ("( … )", "a subpattern with priority"),
            ("a|b", "a or b"),
        }),

        ("Anchors", new (string, string)[]
        {
            ("^", "start of the text"),
            ("$", "end of the text"),
        }),

        ("Examples", new (string, string)[]
        {
            ("colou?r", "matches: color, colour"),
            ("\\d{3}-\\d{4}", "matches: 123-4567"),
            ("^[A-Z][a-z]+$", "whole line: Alice"),
            ("cat|dog", "matches: cat, dog"),
            ("(\\w+)@(\\w+)\\.com", "matches: ivan@gmail.com"),
        }),
    };
}
