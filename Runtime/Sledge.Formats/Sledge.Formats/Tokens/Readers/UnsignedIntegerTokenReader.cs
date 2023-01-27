using System.IO;

namespace Sledge.Formats.Tokens.Readers
{
    /// <summary>
    /// Reads an integer consisting of at least one digit.
    /// Sign prefixes are not included.
    /// </summary>
    public class UnsignedIntegerTokenReader : ITokenReader
    {
        public Token Read(char start, TextReader reader)
        {
            if (start < '0' || start > '9') return null;

            var value = start.ToString();
            int b;
            while ((b = reader.Peek()) >= 0)
            {
                if (b >= '0' && b <= '9')
                {
                    value += (char)b;
                    reader.Read(); // advance the stream
                }
                else
                {
                    break;
                }
            }

            return new Token(TokenType.Number, value);
        }
    }
}