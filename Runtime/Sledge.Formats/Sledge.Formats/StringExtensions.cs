using System;
using System.Collections.Generic;

namespace Sledge.Formats
{
    /// <summary>
    /// Common string extension methods
    /// </summary>
    public static class StringExtensions
    {
        /// <summary>
        /// Split a string by a delimiter without splitting sequences within quotes.
        /// </summary>
        /// <param name="line">The string to split</param>
        /// <param name="splitCharacters">The characters to split by. Defaults to space and tab characters if not specified.</param>
        /// <param name="quoteChar">The character which indicates the start or end of a quote</param>
        /// <param name="escapeChar">The character which indicates that the next character should be escaped</param>
        /// <returns>The split result, with split characters removed</returns>
        public static string[] SplitWithQuotes(this string line, char[] splitCharacters = null, char quoteChar = '"',
            char escapeChar = '\\')
        {
            if (splitCharacters == null) splitCharacters = new[] {' ', '\t'};

            var result = new List<string>();

            char[] builder = new char[line.Length];
            int b = 0;
            var inQuote = false;
            for (var i = 0; i < line.Length; i++)
            {
                var c = line[i];
                if (c == escapeChar)
                {
                    // Escape character, skip the next character
                    i++;
                    if (line.Length == i) throw new InvalidOperationException("Unexpected escape character at end of string");
                    builder[b++] = line[i];
                }
                else if (c == quoteChar && !inQuote)
                {
                    // Quote character to begin a token
                    if (b != 0) throw new InvalidOperationException("Unexpected quote - quotes must be at the beginning of a token");
                    inQuote = true;
                }
                else if (c == quoteChar)
                {
                    // Quote character to end a token
                    inQuote = false;
                    result.Add(new string(builder, 0, b));
                    b = 0;
                    i++;
                    if (line.Length < i && Array.IndexOf(splitCharacters, line[i]) < 0) throw new InvalidOperationException("Missing split character - closing quotes must complete a token");
                }
                else if (!inQuote && Array.IndexOf(splitCharacters, c) >= 0)
                {
                    // Split character outside of quotes, split here
                    if (b > 0) result.Add(new string(builder, 0, b));
                    b = 0;
                }
                else
                {
                    builder[b++] = line[i];
                }
            }

            if (inQuote) throw new InvalidOperationException("Unclosed quote at end of string");
            if (b > 0) result.Add(new string(builder, 0, b));

            return result.ToArray();
        }
    }
}