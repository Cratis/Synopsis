// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;

namespace Cratis.Synopsis.Parsing;

/// <summary>
/// A movable position in JavaScript or TypeScript source with helpers for skipping literals, comments, brackets and whitespace.
/// </summary>
/// <param name="text">The source text.</param>
/// <param name="position">The initial position.</param>
sealed class TypeScriptCursor(string text, int position = 0)
{
    const string RegexPrecedingCharacters = "=(:,[!&|?{};>";

    /// <summary>
    /// The outcome of trying to skip a string, regular expression or comment.
    /// </summary>
    enum Skip
    {
        None,
        Skipped,
        Unterminated
    }

    /// <summary>
    /// Gets the source text.
    /// </summary>
    public string Text { get; } = text;

    /// <summary>
    /// Gets or sets the current position.
    /// </summary>
    public int Position { get; set; } = position;

    /// <summary>
    /// Gets or sets the exclusive end of the range some searches are limited to; defaults to the end of the text.
    /// </summary>
    public int Limit { get; set; } = text.Length;

    /// <summary>
    /// Gets a value indicating whether the position is at the end of the text.
    /// </summary>
    public bool AtEnd => Position >= Text.Length;

    /// <summary>
    /// Gets the number of characters between the position and the limit.
    /// </summary>
    public int RemainingLength => Math.Max(0, Limit - Position);

    char Current => Text[Position];

    char Next => Position + 1 < Text.Length ? Text[Position + 1] : '\0';

    public static bool IsQuote(char character) => character is '\'' or '"' or '`';

    public static bool IsIdentifierCharacter(char character) => char.IsLetterOrDigit(character) || character is '_' or '$';

    public bool At(char expected) => !AtEnd && Current == expected;

    public bool AtQuote() => !AtEnd && IsQuote(Current);

    public void Advance() => Position++;

    public TypeScriptCursor Fork() => new(Text, Position) { Limit = Limit };

    public void SkipWhitespace() => SkipWhile(char.IsWhiteSpace);

    public void SkipLetters() => SkipWhile(char.IsLetter);

    public void SkipIdentifier() => SkipWhile(IsIdentifierCharacter);

    /// <summary>
    /// Finds the bracket closing the parenthesis at the current position, ignoring literals and comments.
    /// </summary>
    /// <returns>The index of the closing parenthesis, or -1 when there is none.</returns>
    public int MatchingParenthesis() => MatchingBracket('(', ')');

    /// <summary>
    /// Finds the brace closing the one at the current position, ignoring literals and comments.
    /// </summary>
    /// <returns>The index of the closing brace, or -1 when there is none.</returns>
    public int MatchingBrace() => MatchingBracket('{', '}');

    /// <summary>
    /// Marks which characters of the text are code rather than comment, string or regular expression text.
    /// </summary>
    public bool[] CodePositions()
    {
        var result = new bool[Text.Length];
        var scan = new TypeScriptCursor(Text);
        while (!scan.AtEnd)
        {
            var skip = scan.SkipNonCode();
            if (skip == Skip.Unterminated)
            {
                break;
            }
            if (skip == Skip.None)
            {
                result[scan.Position] = true;
                scan.Advance();
            }
        }
        return result;
    }

    /// <summary>
    /// Finds the end of the argument starting at the current position: the first top-level comma, or the limit.
    /// </summary>
    public int TopLevelArgumentEnd()
    {
        var scan = Fork();
        var nesting = new Nesting();
        while (scan.Position < Limit)
        {
            if (scan.AtQuote())
            {
                if (!scan.SkipString())
                {
                    return Limit;
                }
                continue;
            }

            if (scan.Current == ',' && nesting.IsTopLevel)
            {
                return scan.Position;
            }
            nesting.Track(scan.Current);
            scan.Advance();
        }
        return Limit;
    }

    /// <summary>
    /// Reads the string literal at the current position and moves past it.
    /// </summary>
    public bool TryReadString(out string? value)
    {
        value = null;
        if (!AtQuote())
        {
            return false;
        }

        var quote = Current;
        var builder = new StringBuilder();
        var escaped = false;
        for (var index = Position + 1; index < Text.Length; index++)
        {
            var character = Text[index];
            if (escaped)
            {
                builder.Append(character);
                escaped = false;
            }
            else if (character == quote)
            {
                value = builder.ToString();
                Position = index + 1;
                return true;
            }
            else if (character == '\\')
            {
                escaped = true;
            }
            else
            {
                builder.Append(character);
            }
        }
        return false;
    }

    /// <summary>
    /// Moves past the string literal at the current position.
    /// </summary>
    /// <returns><c>false</c> when the literal is not terminated, leaving the position unchanged.</returns>
    public bool SkipString()
    {
        var quote = Current;
        var escaped = false;
        for (var index = Position + 1; index < Text.Length; index++)
        {
            if (!escaped && Text[index] == quote)
            {
                Position = index + 1;
                return true;
            }
            escaped = !escaped && Text[index] == '\\';
        }
        return false;
    }

    /// <summary>
    /// Gets the last non-whitespace character before the position on the same line.
    /// </summary>
    public char PreviousNonWhitespaceOnLine()
    {
        for (var cursor = Position - 1; cursor >= 0 && Text[cursor] != '\n'; cursor--)
        {
            if (!char.IsWhiteSpace(Text[cursor]))
            {
                return Text[cursor];
            }
        }
        return '\0';
    }

    /// <summary>
    /// Gets the identifier before the dot that precedes the position, such as <c>test</c> in <c>test.describe</c>.
    /// </summary>
    public string QualifierBefore()
    {
        var dot = Position - 1;
        while (dot >= 0 && char.IsWhiteSpace(Text[dot]))
        {
            dot--;
        }
        if (dot < 0 || Text[dot] != '.')
        {
            return string.Empty;
        }

        var cursor = dot - 1;
        while (cursor >= 0 && IsIdentifierCharacter(Text[cursor]))
        {
            cursor--;
        }
        return Text[(cursor + 1)..dot];
    }

    int MatchingBracket(char open, char close)
    {
        var scan = Fork();
        var depth = 0;
        while (!scan.AtEnd)
        {
            var skip = scan.SkipNonCode();
            if (skip == Skip.Unterminated)
            {
                return -1;
            }
            if (skip == Skip.Skipped)
            {
                continue;
            }

            if (scan.Current == open)
            {
                depth++;
            }
            else if (scan.Current == close && --depth == 0)
            {
                return scan.Position;
            }
            scan.Advance();
        }
        return -1;
    }

    void SkipWhile(Func<char, bool> predicate)
    {
        while (!AtEnd && predicate(Current))
        {
            Position++;
        }
    }

    Skip SkipNonCode()
    {
        if (AtQuote())
        {
            return Outcome(SkipString());
        }
        if (Current != '/')
        {
            return Skip.None;
        }

        return Next switch
        {
            '/' => SkipLineComment(),
            '*' => SkipBlockComment(),
            _ => LooksLikeRegexStart() ? Outcome(SkipRegex()) : Skip.None
        };
    }

    Skip SkipLineComment()
    {
        var newLine = Text.IndexOf('\n', Position);
        Position = newLine < 0 ? Text.Length : newLine + 1;
        return Skip.Skipped;
    }

    Skip SkipBlockComment()
    {
        var close = Text.IndexOf("*/", Position + 2, StringComparison.Ordinal);
        Position = close < 0 ? Text.Length : close + 2;
        return Skip.Skipped;
    }

    bool SkipRegex()
    {
        var escaped = false;
        var characterClass = false;
        for (var index = Position + 1; index < Text.Length; index++)
        {
            var character = Text[index];
            if (escaped)
            {
                escaped = false;
            }
            else if (character == '\\')
            {
                escaped = true;
            }
            else if (character == '[')
            {
                characterClass = true;
            }
            else if (character == ']')
            {
                characterClass = false;
            }
            else if (character == '/' && !characterClass)
            {
                Position = index + 1;
                SkipLetters();
                return true;
            }
        }
        return false;
    }

    bool LooksLikeRegexStart()
    {
        if (!CouldStartRegex())
        {
            return false;
        }
        var previous = PreviousNonWhitespace();
        return previous == '\0' || RegexPrecedingCharacters.Contains(previous);
    }

    bool CouldStartRegex() => Position + 1 < Text.Length && Current == '/' && Next is not ('/' or '*');

    char PreviousNonWhitespace()
    {
        for (var cursor = Position - 1; cursor >= 0; cursor--)
        {
            if (!char.IsWhiteSpace(Text[cursor]))
            {
                return Text[cursor];
            }
        }
        return '\0';
    }

    static Skip Outcome(bool skipped) => skipped ? Skip.Skipped : Skip.Unterminated;

    struct Nesting
    {
        int _round;
        int _square;
        int _curly;

        public readonly bool IsTopLevel => _round == 0 && _square == 0 && _curly == 0;

        public void Track(char character)
        {
            switch (character)
            {
                case '(': _round++; break;
                case ')': _round = Math.Max(0, _round - 1); break;
                case '[': _square++; break;
                case ']': _square = Math.Max(0, _square - 1); break;
                case '{': _curly++; break;
                case '}': _curly = Math.Max(0, _curly - 1); break;
            }
        }
    }
}
