namespace Lab2.RegexLang;

/// <summary>Base class of the regular expression abstract syntax tree.</summary>
public abstract class AstNode
{
}

/// <summary>A single literal character.</summary>
public sealed class LiteralNode : AstNode
{
    public LiteralNode(char value) => Value = value;

    public char Value { get; }
}

/// <summary>The "." wildcard: any character except newline.</summary>
public sealed class AnyNode : AstNode
{
    private AnyNode()
    {
    }

    public static AnyNode Instance { get; } = new();
}

/// <summary>Character class: a set of ranges, optionally negated ("^" at the start).</summary>
public sealed class CharClassNode : AstNode
{
    private readonly (char Min, char Max)[] _ranges;

    public CharClassNode(IEnumerable<(char Min, char Max)> ranges, bool negated)
    {
        _ranges = ranges.ToArray();
        Negated = negated;
    }

    public bool Negated { get; }

    public bool Matches(char c)
    {
        bool inside = false;
        foreach (var (min, max) in _ranges)
        {
            if (c >= min && c <= max)
            {
                inside = true;
                break;
            }
        }

        return Negated ? !inside : inside;
    }

    internal static CharClassNode FromPredef(PredefKind kind) =>
        new(
            RangesFor(kind),
            kind is PredefKind.NotDigit or PredefKind.NotWord or PredefKind.NotSpace);

    /// <summary>
    /// Parses the character class body (the text between "[" and "]"): literals, escapes,
    /// predefined classes (\d, \w, \s) and ranges such as a-z.
    /// <paramref name="basePos"/> is the index of the first body character in the original
    /// source and is used for error messages.
    /// </summary>
    internal static CharClassNode FromClassBody(string body, bool negated, int basePos)
    {
        var ranges = new List<(char Min, char Max)>();
        int i = 0;

        while (i < body.Length)
        {
            int elementPos = basePos + i;
            var first = ReadElement(body, ref i, basePos);

            // A range: element '-', followed by another element (if anything follows the '-').
            if (i < body.Length && body[i] == '-' && i + 1 < body.Length)
            {
                i++; // skip '-'
                var second = ReadElement(body, ref i, basePos);

                if (first.IsPredef || second.IsPredef)
                {
                    throw new RegexSyntaxException(
                        @"range boundaries must be single characters: predefined classes \d, \w, \s are not allowed in ranges",
                        elementPos);
                }

                if (first.Value > second.Value)
                {
                    throw new RegexSyntaxException(
                        $"invalid range: '{first.Value}' is greater than '{second.Value}'",
                        elementPos);
                }

                ranges.Add((first.Value, second.Value));
            }
            else if (first.IsPredef)
            {
                ranges.AddRange(RangesFor(first.Predef));
            }
            else
            {
                ranges.Add((first.Value, first.Value));
            }
        }

        return new CharClassNode(ranges, negated);
    }

    private static (bool IsPredef, char Value, PredefKind Predef) ReadElement(
        string body, ref int i, int basePos)
    {
        char c = body[i];
        if (c != '\\')
        {
            i++;
            return (false, c, default);
        }

        if (i + 1 >= body.Length)
            throw new RegexSyntaxException("unterminated escape sequence in character class", basePos + i);

        char e = body[i + 1];
        i += 2;
        return e switch
        {
            'd' => (true, '\0', PredefKind.Digit),
            'D' => (true, '\0', PredefKind.NotDigit),
            'w' => (true, '\0', PredefKind.Word),
            'W' => (true, '\0', PredefKind.NotWord),
            's' => (true, '\0', PredefKind.Space),
            'S' => (true, '\0', PredefKind.NotSpace),
            'n' => (false, '\n', default),
            't' => (false, '\t', default),
            'r' => (false, '\r', default),
            'f' => (false, '\f', default),
            'v' => (false, '\v', default),
            _ when char.IsLetter(e) =>
                throw new RegexSyntaxException(
                    $"unknown escape sequence \\{e} in character class",
                    basePos + i - 2),
            _ => (false, e, default),
        };
    }

    internal static (char Min, char Max)[] RangesFor(PredefKind kind) => kind switch
    {
        PredefKind.Digit or PredefKind.NotDigit => new[] { ('0', '9') },
        PredefKind.Word or PredefKind.NotWord => new[] { ('0', '9'), ('A', 'Z'), ('a', 'z'), ('_', '_') },
        PredefKind.Space or PredefKind.NotSpace =>
            new[] { (' ', ' '), ('\t', '\t'), ('\n', '\n'), ('\r', '\r'), ('\f', '\f'), ('\v', '\v') },
        _ => Array.Empty<(char, char)>(),
    };
}

/// <summary>A sequence of nodes (concatenation).</summary>
public sealed class ConcatNode : AstNode
{
    public ConcatNode(IReadOnlyList<AstNode> items) => Items = items;

    public IReadOnlyList<AstNode> Items { get; }
}

/// <summary>Alternation: "a|b|c".</summary>
public sealed class AlternateNode : AstNode
{
    public AlternateNode(IReadOnlyList<AstNode> options) => Options = options;

    public IReadOnlyList<AstNode> Options { get; }
}

/// <summary>Repetition: the greedy quantifiers * + ? {m} {m,} {m,n}.</summary>
public sealed class RepeatNode : AstNode
{
    public RepeatNode(AstNode child, int min, int max)
    {
        Child = child;
        Min = min;
        Max = max;
    }

    public AstNode Child { get; }

    /// <summary>Minimum number of repetitions.</summary>
    public int Min { get; }

    /// <summary>Maximum number of repetitions (<see cref="int.MaxValue"/> means unbounded).</summary>
    public int Max { get; }
}

/// <summary>A group "(...)" that sets subpattern precedence.</summary>
public sealed class GroupNode : AstNode
{
    public GroupNode(AstNode child) => Child = child;

    public AstNode Child { get; }
}

/// <summary>The "^" anchor: start of the text.</summary>
public sealed class AnchorStartNode : AstNode
{
    private AnchorStartNode()
    {
    }

    public static AnchorStartNode Instance { get; } = new();
}

/// <summary>The "$" anchor: end of the text.</summary>
public sealed class AnchorEndNode : AstNode
{
    private AnchorEndNode()
    {
    }

    public static AnchorEndNode Instance { get; } = new();
}
