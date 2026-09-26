using System.Text;

namespace AFK4.Agent.Service.Cleanup;

/// <summary>
/// Минимум формата KeyValues Steam (<c>config.vdf</c>): ключи и значения в кавычках, вложенность —
/// фигурными скобками, <c>\</c> экранирует символ внутри кавычек. Разбирать файл целиком не нужно:
/// из него вырезается один блок, остальное остаётся байт в байт.
/// </summary>
public static class VdfText
{
    /// <summary>Вырезать все блоки <c>"key" { … }</c> с этим ключом на любой глубине.</summary>
    public static string RemoveBlocks(string text, string key, out int removed)
    {
        removed = 0;
        var output = new StringBuilder(text.Length);
        var index = 0;
        while (index < text.Length)
        {
            var character = text[index];
            if (character == '/' && index + 1 < text.Length && text[index + 1] == '/')
            {
                var lineEnd = text.IndexOf('\n', index);
                lineEnd = lineEnd < 0 ? text.Length : lineEnd;
                output.Append(text, index, lineEnd - index);
                index = lineEnd;
                continue;
            }

            if (character != '"')
            {
                output.Append(character);
                index++;
                continue;
            }

            var tokenEnd = QuotedEnd(text, index);
            var token = text[(index + 1)..(tokenEnd - 1)];
            var afterToken = SkipWhitespace(text, tokenEnd);
            if (string.Equals(token, key, StringComparison.OrdinalIgnoreCase) && afterToken < text.Length && text[afterToken] == '{')
            {
                var blockEnd = BlockEnd(text, afterToken);
                // Вместе с блоком уходит и отступ строки, на которой стоял ключ: файл остаётся ровным.
                TrimTrailingIndent(output);
                index = SkipLineBreak(text, blockEnd);
                removed++;
                continue;
            }

            output.Append(text, index, tokenEnd - index);
            index = tokenEnd;
        }

        return output.ToString();
    }

    private static int QuotedEnd(string text, int start)
    {
        var index = start + 1;
        while (index < text.Length)
        {
            if (text[index] == '\\')
            {
                index += 2;
                continue;
            }

            if (text[index] == '"')
            {
                return index + 1;
            }

            index++;
        }

        return text.Length;
    }

    private static int BlockEnd(string text, int openBrace)
    {
        var depth = 0;
        var index = openBrace;
        while (index < text.Length)
        {
            switch (text[index])
            {
                case '"':
                    index = QuotedEnd(text, index);
                    continue;
                case '{':
                    depth++;
                    break;
                case '}':
                    depth--;
                    if (depth == 0)
                    {
                        return index + 1;
                    }

                    break;
            }

            index++;
        }

        return text.Length;
    }

    private static int SkipWhitespace(string text, int index)
    {
        while (index < text.Length && char.IsWhiteSpace(text[index]))
        {
            index++;
        }

        return index;
    }

    private static int SkipLineBreak(string text, int index)
    {
        if (index < text.Length && text[index] == '\r')
        {
            index++;
        }

        if (index < text.Length && text[index] == '\n')
        {
            index++;
        }

        return index;
    }

    private static void TrimTrailingIndent(StringBuilder output)
    {
        while (output.Length > 0 && output[^1] is ' ' or '\t')
        {
            output.Length--;
        }
    }
}
