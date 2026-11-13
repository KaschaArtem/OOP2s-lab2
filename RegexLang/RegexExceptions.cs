namespace Lab2.RegexLang;

/// <summary>Error raised while parsing an expression in the custom regular language.</summary>
public sealed class RegexSyntaxException : Exception
{
    public RegexSyntaxException(string message, int position) : base(message)
        => Position = position;

    /// <summary>Zero-based index of the character where the error was detected.</summary>
    public int Position { get; }
}

/// <summary>The expression is too complex to check: the computation limit was exceeded.</summary>
public sealed class MatchComplexityException : Exception
{
    public MatchComplexityException()
        : base("The expression is too complex: computation limit exceeded. Simplify the expression.")
    {
    }
}
