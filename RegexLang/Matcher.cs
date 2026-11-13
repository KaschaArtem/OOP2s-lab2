namespace Lab2.RegexLang;

/// <summary>A found match: a range of characters inside the searched string.</summary>
public readonly record struct MatchSpan(int Start, int Length);

/// <summary>
/// Searches for pattern matches in a string. Own implementation (recursive backtracking
/// over the AST); System.Text.RegularExpressions is intentionally not used.
/// </summary>
public static class RegexpMatcher
{
    /// <summary>
    /// Finds all non-overlapping matches from left to right.
    /// Throws <see cref="MatchComplexityException"/> when the expression is too complex.
    /// </summary>
    public static IReadOnlyList<MatchSpan> FindAll(string text, AstNode pattern)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(pattern);

        return new Matcher(text, pattern).FindAll();
    }

    private sealed class Matcher
    {
        /// <summary>Overall step budget for one search — guards against backtracking explosions.</summary>
        private const int BudgetLimit = 20_000_000;

        private readonly string _text;
        private readonly AstNode _pattern;
        private int _budget;
        private int _matchEnd;

        public Matcher(string text, AstNode pattern)
        {
            _text = text;
            _pattern = pattern;
        }

        public IReadOnlyList<MatchSpan> FindAll()
        {
            var result = new List<MatchSpan>();
            _budget = BudgetLimit;

            int pos = 0;
            while (pos <= _text.Length)
            {
                _matchEnd = -1;
                if (Match(_pattern, pos, RecordEnd) && _matchEnd > pos)
                {
                    // Non-overlapping matches: resume right after the previous one.
                    result.Add(new MatchSpan(pos, _matchEnd - pos));
                    pos = _matchEnd;
                }
                else
                {
                    pos++;
                }
            }

            return result;
        }

        private bool RecordEnd(int pos)
        {
            _matchEnd = pos;
            return true;
        }

        private bool Match(AstNode node, int pos, Continuation k)
        {
            if (--_budget <= 0)
                throw new MatchComplexityException();

            switch (node)
            {
                case LiteralNode literal:
                    return pos < _text.Length && _text[pos] == literal.Value && k(pos + 1);

                case AnyNode:
                    return pos < _text.Length && _text[pos] != '\n' && k(pos + 1);

                case CharClassNode charClass:
                    return pos < _text.Length && charClass.Matches(_text[pos]) && k(pos + 1);

                case AnchorStartNode:
                    return pos == 0 && k(pos);

                case AnchorEndNode:
                    return pos == _text.Length && k(pos);

                case GroupNode group:
                    return Match(group.Child, pos, k);

                case ConcatNode concat:
                    return MatchSequence(concat.Items, 0, pos, k);

                case AlternateNode alternate:
                    foreach (var option in alternate.Options)
                    {
                        if (Match(option, pos, k))
                            return true;
                    }

                    return false;

                case RepeatNode repeat:
                    return MatchRepeat(repeat, pos, 0, k);

                default:
                    return false;
            }
        }

        private bool MatchSequence(IReadOnlyList<AstNode> items, int index, int pos, Continuation k)
        {
            if (index == items.Count)
                return k(pos);

            return Match(items[index], pos, p => MatchSequence(items, index + 1, p, k));
        }

        private bool MatchRepeat(RepeatNode repeat, int pos, int count, Continuation k)
        {
            // Until the minimum is reached, consuming one more repetition is mandatory.
            if (count < repeat.Min)
            {
                return Match(repeat.Child, pos, p =>
                {
                    // The subpattern consumed nothing — stop here to avoid an infinite loop.
                    if (p == pos)
                        return k(p);

                    return MatchRepeat(repeat, p, count + 1, k);
                });
            }

            // Greediness: first try to consume one more repetition, then give up.
            if (count < repeat.Max &&
                Match(repeat.Child, pos, p => p > pos && MatchRepeat(repeat, p, count + 1, k)))
            {
                return true;
            }

            return k(pos);
        }

        private delegate bool Continuation(int pos);
    }
}
