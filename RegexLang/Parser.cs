namespace Lab2.RegexLang;

/// <summary>
/// Parser for the custom regular language. Grammar:
///   alternation := concatenation ('|' concatenation)*
///   concatenation := repeat*
///   repeat      := atom quantifier?
///   atom        := '(' alternation ')' | '[' class ']' | '.' | '^' | '$'
///                | literal | predefined class
///   quantifier  := '*' | '+' | '?' | '{m}' | '{m,}' | '{m,n}'
/// </summary>
public static class RegexpParser
{
    /// <summary>Parses an expression and builds the AST. Throws <see cref="RegexSyntaxException"/>.</summary>
    public static AstNode Parse(string pattern)
    {
        ArgumentNullException.ThrowIfNull(pattern);

        var tokens = new Lexer(pattern).Tokenize();
        var parser = new Parser(tokens);
        var node = parser.ParseAlternation();

        if (parser.Current.Kind == TokenKind.CloseParen)
            throw new RegexSyntaxException("unexpected closing parenthesis ')'", parser.Current.Position);

        return node;
    }

    private sealed class Parser
    {
        private readonly IReadOnlyList<Token> _tokens;
        private int _index;

        public Parser(IReadOnlyList<Token> tokens) => _tokens = tokens;

        internal Token Current => _tokens[_index];

        private Token Advance() => _tokens[_index++];

        public AstNode ParseAlternation()
        {
            var options = new List<AstNode> { ParseConcat() };

            while (Current.Kind == TokenKind.Pipe)
            {
                Advance();
                options.Add(ParseConcat());
            }

            return options.Count == 1 ? options[0] : new AlternateNode(options);
        }

        private AstNode ParseConcat()
        {
            var items = new List<AstNode>();

            while (Current.Kind is not (TokenKind.Pipe or TokenKind.CloseParen or TokenKind.End))
                items.Add(ParseRepeat());

            if (items.Count == 0)
            {
                throw Current.Kind switch
                {
                    TokenKind.End => new RegexSyntaxException("empty subexpression", Current.Position),
                    TokenKind.Pipe => new RegexSyntaxException(
                        "empty subexpression before or after '|'", Current.Position),
                    _ => new RegexSyntaxException(
                        "empty subexpression before ')'", Current.Position),
                };
            }

            return items.Count == 1 ? items[0] : new ConcatNode(items);
        }

        private AstNode ParseRepeat()
        {
            var atom = ParseAtom();
            var token = Current;

            if (token.Kind is not (TokenKind.Star or TokenKind.Plus or TokenKind.Question or TokenKind.Repeat))
                return atom;

            if (atom is AnchorStartNode or AnchorEndNode)
            {
                string anchor = atom is AnchorStartNode ? "^" : "$";
                throw new RegexSyntaxException($"anchor {anchor} cannot be quantified", token.Position);
            }

            Advance();

            var (min, max) = token.Kind switch
            {
                TokenKind.Star => (0, int.MaxValue),
                TokenKind.Plus => (1, int.MaxValue),
                TokenKind.Question => (0, 1),
                _ => (token.RepeatMin, token.RepeatMax < 0 ? int.MaxValue : token.RepeatMax),
            };

            return new RepeatNode(atom, min, max);
        }

        private AstNode ParseAtom()
        {
            var token = Advance();

            switch (token.Kind)
            {
                case TokenKind.Char:
                    return new LiteralNode(token.CharValue);

                case TokenKind.Predef:
                    return CharClassNode.FromPredef(token.Predef);

                case TokenKind.Dot:
                    return AnyNode.Instance;

                case TokenKind.Class:
                    // Body offset: '[' plus an optional '^'.
                    int bodyBase = token.Position + 1 + (token.ClassNegated ? 1 : 0);
                    return CharClassNode.FromClassBody(token.ClassBody, token.ClassNegated, bodyBase);

                case TokenKind.OpenParen:
                {
                    var inner = ParseAlternation();
                    if (Current.Kind != TokenKind.CloseParen)
                        throw new RegexSyntaxException("unclosed parenthesis '('", token.Position);
                    Advance();
                    return new GroupNode(inner);
                }

                case TokenKind.AnchorStart:
                    return AnchorStartNode.Instance;

                case TokenKind.AnchorEnd:
                    return AnchorEndNode.Instance;

                case TokenKind.CloseParen:
                    throw new RegexSyntaxException("unexpected closing parenthesis ')'", token.Position);

                case TokenKind.Star:
                case TokenKind.Plus:
                case TokenKind.Question:
                case TokenKind.Repeat:
                    throw new RegexSyntaxException("quantifier has nothing to repeat", token.Position);

                default:
                    throw new RegexSyntaxException("unexpected end of expression", token.Position);
            }
        }
    }
}
