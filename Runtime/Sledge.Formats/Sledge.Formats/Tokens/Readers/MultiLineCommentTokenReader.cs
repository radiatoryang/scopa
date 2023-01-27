using System;
using System.IO;
using System.Text;

namespace Sledge.Formats.Tokens.Readers
{
    /// <summary>
    /// Reads a comment surrounded by `/*` and `*/`
    /// </summary>
    public class MultiLineCommentTokenReader : ITokenReader
    {
        public Token Read(char start, TextReader reader)
        {
            if (start != '/' || reader.Peek() != '*') return null;

            var b = reader.Read();
            if (b != '*') throw new InvalidOperationException($"Expected *, got {b} instead.");

            // It's a comment, read everything until we hit the closing tag
            var text = new StringBuilder();
            while ((b = reader.Read()) >= 0)
            {
                if (b == '\r') continue;
                if (b == '*' && reader.Peek() == '/')
                {
                    b = reader.Read();
                    if (b != '/') throw new InvalidOperationException($"Expected /, got {b} instead.");
                    return new Token(TokenType.Comment, text.ToString());
                }
                text.Append((char) b);
            }

            return new Token(TokenType.Invalid, "Unterminated multi-line comment.");
        }
    }
}