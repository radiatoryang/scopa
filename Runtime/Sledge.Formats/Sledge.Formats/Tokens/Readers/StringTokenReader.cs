using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Sledge.Formats.Tokens.Readers
{
    /// <summary>
    /// Reads a string surrounded by a quote character such as "
    /// </summary>
    public class StringTokenReader : ITokenReader
    {
        public char QuoteCharacter { get; set; } = '"';
        public bool AllowNewlines { get; set; } = false;
        public bool AllowEscaping { get; set; } = true;
        public Dictionary<char, char> EscapedCharacters { get; set; } = new Dictionary<char, char>
        {
            {'n', '\n'},
            {'r', '\r'},
            {'t', '\t'},
        };

        public Token Read(char start, TextReader reader)
        {
            if (start != QuoteCharacter) return null;
            var sb = new StringBuilder();
            int b;
            while ((b = reader.Read()) >= 0)
            {
                // Can't use `QuoteCharacter` in a switch case as it's not constant
                if (b == QuoteCharacter)
                {
                    // End of string
                    return new Token(TokenType.String, sb.ToString());
                }
                switch (b)
                {
                    // ignore carriage returns
                    case '\r':
                        continue;
                    // Newline in string (when they're not allowed)
                    case '\n' when !AllowNewlines:
                        // Syntax error, unterminated string
                        return new Token(TokenType.String, sb.ToString())
                        {
                            Warnings =
                            {
                                "String cannot contain a newline"
                            }
                        };
                    // Escaped character (when allowed)
                    case '\\' when AllowEscaping:
                    {
                        // Read the next character
                        b = reader.Read();
                        // EOF reached
                        if (b < 0) return new Token(TokenType.Invalid, "Unexpected end of file while reading string value");
                        // Check the dictionary for escaped chars, if it's not there just use whatever character it is (e.g. `\\` or `\"`)
                        if (EscapedCharacters.ContainsKey((char)b)) sb.Append(EscapedCharacters[(char)b]);
                        else sb.Append((char)b);
                        break;
                    }
                    // Any other character
                    default:
                        sb.Append((char)b);
                        break;
                }
            }

            return new Token(TokenType.Invalid, "Unexpected end of file while reading string value");
        }
    }
}