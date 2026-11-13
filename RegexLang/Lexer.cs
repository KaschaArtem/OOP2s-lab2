namespace Lab2.RegexLang;

internal enum TokenKind
{
    Char,        // literal or escaped character
    Predef,      // predefined class: \d \D \w \W \s \S
    Dot,         // .
    Star,        // *
    Plus,        // +
    Question,    // ?
    Pipe,        // |
    OpenParen,   // (
    CloseParen,  // )
    Class,       // [ ... ]
    AnchorStart, // ^
    AnchorEnd,   // $
    Repeat,      // {m}, {m,} or {m,n}
    End,         // end of the expression
}

internal enum PredefKind
{
    Digit,
    NotDigit,
    Word,
    NotWord,
    Space,
    NotSpace,
}

internal sealed class Token
{
    public TokenKind Kind { get; init; }

    /// <summary>Zero-based index of the token start in the original expression.</summary>
    public int Position { get; init; }

    public char CharValue { get; init; }

    public PredefKind Predef { get; init; }

    /// <summary>The character class body without the square brackets.</summary>
    public string ClassBody { get; init; } = string.Empty;

    public bool ClassNegated { get; init; }

    public int RepeatMin { get; init; }

    /// <summary>-1 when no upper bound is given (for example "{2,}").</summary>
    public int RepeatMax { get; init; } = -1;
}

/// <summary>
/// Lexer for the custom regular language: splits an expression into tokens, recognizes
/// escapes, character classes and {m,n} quantifiers, and reports errors with the exact
/// position of the offending character.
/// </summary>
internal sealed class Lexer
{
    private readonly string _source;
    private readonly List<Token> _tokens = new();
    private int _pos;

    public Lexer(string source) => _source = source;

    public List<Token> Tokenize()
    {
        while (_pos < _source.Length)
        {
            char c = _source[_pos];
            switch (c)
            {
                case '\\':
                    ReadEscape();
                    break;
                case '.':
                    AddSimple(TokenKind.Dot);
                    break;
                case '*':
                    AddSimple(TokenKind.Star);
                    break;
                case '+':
                    AddSimple(TokenKind.Plus);
                    break;
                case '?':
                    AddSimple(TokenKind.Question);
                    break;
                case '|':
                    AddSimple(TokenKind.Pipe);
                    break;
                case '(':
                    AddSimple(TokenKind.OpenParen);
                    break;
                case ')':
                    AddSimple(TokenKind.CloseParen);
                    break;
                case '^':
                    AddSimple(TokenKind.AnchorStart);
                    break;
                case '$':
                    AddSimple(TokenKind.AnchorEnd);
                    break;
                case '[':
                    ReadClass();
                    break;
                case '{':
                    ReadRepeat();
                    break;
                default:
                    _tokens.Add(new Token { Kind = TokenKind.Char, Position = _pos, CharValue = c });
                    _pos++;
                    break;
            }
        }

        _tokens.Add(new Token { Kind = TokenKind.End, Position = _source.Length });
        return _tokens;
    }

    private void AddSimple(TokenKind kind)
    {
        _tokens.Add(new Token { Kind = kind, Position = _pos });
        _pos++;
    }

    private void ReadEscape()
    {
        int start = _pos;
        _pos++; // skip the '\'

        if (_pos >= _source.Length)
            throw new RegexSyntaxException("unterminated escape sequence at the end of the expression", start);

        char c = _source[_pos];
        _pos++;

        Token token = c switch
        {
            'd' => new Token { Kind = TokenKind.Predef, Position = start, Predef = PredefKind.Digit },
            'D' => new Token { Kind = TokenKind.Predef, Position = start, Predef = PredefKind.NotDigit },
            'w' => new Token { Kind = TokenKind.Predef, Position = start, Predef = PredefKind.Word },
            'W' => new Token { Kind = TokenKind.Predef, Position = start, Predef = PredefKind.NotWord },
            's' => new Token { Kind = TokenKind.Predef, Position = start, Predef = PredefKind.Space },
            'S' => new Token { Kind = TokenKind.Predef, Position = start, Predef = PredefKind.NotSpace },
            'n' => new Token { Kind = TokenKind.Char, Position = start, CharValue = '\n' },
            't' => new Token { Kind = TokenKind.Char, Position = start, CharValue = '\t' },
            'r' => new Token { Kind = TokenKind.Char, Position = start, CharValue = '\r' },
            'f' => new Token { Kind = TokenKind.Char, Position = start, CharValue = '\f' },
            'v' => new Token { Kind = TokenKind.Char, Position = start, CharValue = '\v' },
            _ when char.IsLetter(c) =>
                throw new RegexSyntaxException($"unknown escape sequence \\{c}", start),
            _ => new Token { Kind = TokenKind.Char, Position = start, CharValue = c },
        };

        _tokens.Add(token);
    }

    private void ReadClass()
    {
        int start = _pos;
        _pos++; // skip the '['

        bool negated = false;
        if (_pos < _source.Length && _source[_pos] == '^')
        {
            negated = true;
            _pos++;
        }

        // The class body starts here; a ']' in the first position is a literal.
        int bodyStart = _pos;
        if (_pos < _source.Length && _source[_pos] == ']')
            _pos++;

        bool closed = false;
        while (_pos < _source.Length)
        {
            if (_source[_pos] == '\\')
            {
                _pos += 2; // an escaped character may be ']'
                continue;
            }

            if (_source[_pos] == ']')
            {
                closed = true;
                _pos++;
                break;
            }

            _pos++;
        }

        if (!closed)
            throw new RegexSyntaxException("unclosed character class", start);

        string body = _source[bodyStart..(_pos - 1)];
        _tokens.Add(new Token
        {
            Kind = TokenKind.Class,
            Position = start,
            ClassBody = body,
            ClassNegated = negated,
        });
    }

    private void ReadRepeat()
    {
        int start = _pos;
        int j = _pos + 1;

        int digitsStart = j;
        while (j < _source.Length && char.IsDigit(_source[j]))
            j++;

        if (j == digitsStart)
            throw new RegexSyntaxException("expected a number after '{'", start);

        int min = ParseNumber(digitsStart, j - digitsStart, start);

        // By default "{m}" means "exactly m". A comma makes the upper bound optional:
        // "{m,}" is unbounded, "{m,n}" is a range.
        int max = min;
        if (j < _source.Length && _source[j] == ',')
        {
            j++;
            int secondDigitsStart = j;
            while (j < _source.Length && char.IsDigit(_source[j]))
                j++;

            max = j > secondDigitsStart
                ? ParseNumber(secondDigitsStart, j - secondDigitsStart, start)
                : -1;
        }

        if (j >= _source.Length || _source[j] != '}')
            throw new RegexSyntaxException("malformed quantifier: expected '}'", start);

        j++;

        if (max >= 0 && min > max)
        {
            throw new RegexSyntaxException(
                $"minimum repeat count ({min}) is greater than maximum ({max})",
                start);
        }

        _pos = j;
        _tokens.Add(new Token
        {
            Kind = TokenKind.Repeat,
            Position = start,
            RepeatMin = min,
            RepeatMax = max,
        });
    }

    private int ParseNumber(int start, int length, int tokenPosition)
    {
        if (!int.TryParse(_source.Substring(start, length), out int value))
            throw new RegexSyntaxException("repeat count is too large", tokenPosition);

        return value;
    }
}
