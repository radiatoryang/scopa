using System;
using System.IO;
using System.Text;

namespace Sledge.Formats.Tokens.Readers
{
    /// <summary>
    /// Reads a comment prefixed by `//`
    /// </summary>
    public class SingleLineCommentTokenReader : ITokenReader
    {
        public Token Read(char start, TextReader reader)
        {
            if (start != '/' || reader.Peek() != '/') return null;

            var b = reader.Read();
            if (b != '/') throw new InvalidOperationException($"Expected /, got {b} instead.");

            // It's a comment, read everything until we hit a newline
            var text = new StringBuilder();
            while ((b = reader.Peek()) >= 0)
            {
                if (b == '\n') break; // don't consume the newline

                b = reader.Read();
                if (b == '\r') continue;
                text.Append((char) b);
            }
            return new Token(TokenType.Comment, text.ToString());

        }
    }
}