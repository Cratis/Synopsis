// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;

namespace Cratis.Synopsis.Parsing;

/// <summary>
/// Character-level scanning helpers for JavaScript and TypeScript source: literals, comments, brackets and whitespace.
/// </summary>
static class TypeScriptScanner
{
    const string RegexPrecedingCharacters = "=(:,[!&|?{};>";

    public static bool IsQuote(char character) => character is '\'' or '"' or '`';

    public static bool IsIdentifierCharacter(char character) => char.IsLetterOrDigit(character) || character is '_' or '$';

    public static bool At(string source, int index, char expected) => index < source.Length && source[index] == expected;

    public static int SkipWhitespace(string source, int cursor)
    {
        while (cursor < source.Length && char.IsWhiteSpace(source[cursor]))
        {
            cursor++;
        }
        return cursor;
    }

    public static int SkipLetters(string source, int cursor)
    {
        while (cursor < source.Length && char.IsLetter(source[cursor]))
        {
            cursor++;
        }
        return cursor;
    }

    public static int SkipIdentifier(string source, int cursor)
    {
        while (cursor < source.Length && IsIdentifierCharacter(source[cursor]))
        {
            cursor++;
        }
        return cursor;
    }

    /// <summary>
    /// Finds the index of the bracket closing the one at <paramref name="open"/>, ignoring literals and comments.
    /// </summary>
    /// <returns>The index of the closing bracket, or -1 when there is none.</returns>
    public static int Matching(string source, int open, char openCharacter, char closeCharacter)
    {
        var depth = 0;
        for (var index = open; index < source.Length; index++)
        {
            var after = SkipNonCode(source, index);
            if (after < 0)
            {
                return -1;
            }
            if (after > index)
            {
                index = after - 1;
                continue;
            }

            var character = source[index];
            if (character == openCharacter)
            {
                depth++;
            }
            else if (character == closeCharacter && --depth == 0)
            {
                return index;
            }
        }
        return -1;
    }

    /// <summary>
    /// Marks which characters of the source are code rather than comment, string or regular expression text.
    /// </summary>
    public static bool[] CodePositions(string source)
    {
        var result = new bool[source.Length];
        for (var index = 0; index < source.Length; index++)
        {
            var after = SkipNonCode(source, index);
            if (after < 0)
            {
                break;
            }
            if (after > index)
            {
                index = after - 1;
                continue;
            }
            result[index] = true;
        }
        return result;
    }

    /// <summary>
    /// Finds the end of the argument starting at <paramref name="start"/>: the first top-level comma, or <paramref name="limit"/>.
    /// </summary>
    public static int TopLevelArgumentEnd(string source, int start, int limit)
    {
        var nesting = new Nesting();
        for (var index = start; index < limit; index++)
        {
            if (IsQuote(source[index]))
            {
                var end = StringEnd(source, index);
                if (end < 0)
                {
                    return limit;
                }
                index = end - 1;
                continue;
            }

            if (source[index] == ',' && nesting.IsTopLevel)
            {
                return index;
            }
            nesting.Track(source[index]);
        }
        return limit;
    }

    public static bool TryReadString(string source, int start, out string? value, out int end)
    {
        value = null;
        end = start;
        if (start >= source.Length || !IsQuote(source[start]))
        {
            return false;
        }

        var quote = source[start];
        var builder = new StringBuilder();
        var escaped = false;
        for (var index = start + 1; index < source.Length; index++)
        {
            var character = source[index];
            if (escaped)
            {
                builder.Append(character);
                escaped = false;
            }
            else if (character == quote)
            {
                value = builder.ToString();
                end = index + 1;
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
    /// Finds the index after the string literal starting at <paramref name="start"/>.
    /// </summary>
    /// <returns>The index after the closing quote, or -1 when the literal is not terminated.</returns>
    public static int StringEnd(string source, int start)
    {
        var quote = source[start];
        var escaped = false;
        for (var index = start + 1; index < source.Length; index++)
        {
            if (!escaped && source[index] == quote)
            {
                return index + 1;
            }
            escaped = !escaped && source[index] == '\\';
        }
        return -1;
    }

    /// <summary>
    /// Finds the index after the regular expression literal (and its flags) starting at <paramref name="start"/>.
    /// </summary>
    /// <returns>The index after the literal, or -1 when it is not terminated.</returns>
    public static int RegexEnd(string source, int start)
    {
        var escaped = false;
        var characterClass = false;
        for (var index = start + 1; index < source.Length; index++)
        {
            var character = source[index];
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
                return SkipLetters(source, index + 1);
            }
        }
        return -1;
    }

    public static char PreviousNonWhitespaceOnLine(string source, int index)
    {
        for (var cursor = index - 1; cursor >= 0 && source[cursor] != '\n'; cursor--)
        {
            if (!char.IsWhiteSpace(source[cursor]))
            {
                return source[cursor];
            }
        }
        return '\0';
    }

    /// <summary>
    /// Gets the identifier before the dot that precedes <paramref name="index"/>, such as <c>test</c> in <c>test.describe</c>.
    /// </summary>
    public static string QualifierBefore(string source, int index)
    {
        var dot = index - 1;
        while (dot >= 0 && char.IsWhiteSpace(source[dot]))
        {
            dot--;
        }
        if (dot < 0 || source[dot] != '.')
        {
            return string.Empty;
        }

        var cursor = dot - 1;
        while (cursor >= 0 && IsIdentifierCharacter(source[cursor]))
        {
            cursor--;
        }
        return source[(cursor + 1)..dot];
    }

    /// <summary>
    /// Skips a string, regular expression or comment starting at <paramref name="index"/>.
    /// </summary>
    /// <returns>The index after the construct, <paramref name="index"/> when none starts there, or -1 when a literal is not terminated.</returns>
    static int SkipNonCode(string source, int index)
    {
        var character = source[index];
        if (IsQuote(character))
        {
            return StringEnd(source, index);
        }
        if (character != '/')
        {
            return index;
        }

        var next = index + 1 < source.Length ? source[index + 1] : '\0';
        return next switch
        {
            '/' => LineCommentEnd(source, index),
            '*' => BlockCommentEnd(source, index),
            _ => LooksLikeRegexStart(source, index) ? RegexEnd(source, index) : index
        };
    }

    static int LineCommentEnd(string source, int index)
    {
        var newLine = source.IndexOf('\n', index);
        return newLine < 0 ? source.Length : newLine + 1;
    }

    static int BlockCommentEnd(string source, int index)
    {
        var close = source.IndexOf("*/", index + 2, StringComparison.Ordinal);
        return close < 0 ? source.Length : close + 2;
    }

    static bool LooksLikeRegexStart(string source, int index)
    {
        if (!CouldStartRegex(source, index))
        {
            return false;
        }
        var previous = PreviousNonWhitespace(source, index);
        return previous == '\0' || RegexPrecedingCharacters.Contains(previous);
    }

    static bool CouldStartRegex(string source, int index) =>
        index + 1 < source.Length && source[index] == '/' && source[index + 1] is not ('/' or '*');

    static char PreviousNonWhitespace(string source, int index)
    {
        for (var cursor = index - 1; cursor >= 0; cursor--)
        {
            if (!char.IsWhiteSpace(source[cursor]))
            {
                return source[cursor];
            }
        }
        return '\0';
    }

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
