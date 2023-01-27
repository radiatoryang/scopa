using System;
using System.IO;

namespace Sledge.Formats.Tokens.Readers
{
    /// <summary>
    /// Reads a name starting with a-z, A-Z or `_` and followed by a-z, A-Z, `_`, `-`, or `.`.
    /// These conditions can be customised with the `IsValidStartCharacter` and `IsValidContinuationCharacter` predicates.
    /// </summary>
    public class NameTokenReader : ITokenReader
    {
        public Predicate<char> IsValidStartCharacter { get; set; }
        public Predicate<char> IsValidContinuationCharacter { get; set; }

        public NameTokenReader()
        {
            IsValidStartCharacter = c => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || c == '_';
            IsValidContinuationCharacter = c => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_' || c == '-' || c == '.';
        }

        public NameTokenReader(Predicate<char> isValidStartCharacter, Predicate<char> isValidContinuationCharacter)
        {
            IsValidStartCharacter = isValidStartCharacter;
            IsValidContinuationCharacter = isValidContinuationCharacter;
        }

        public Token Read(char start, TextReader reader)
        {
            if (!IsValidStartCharacter(start)) return null;

            var name = start.ToString();
            int b;
            while ((b = reader.Peek()) >= 0)
            {
                var c = (char)b;
                if (IsValidContinuationCharacter(c))
                {
                    name += c;
                    reader.Read(); // advance the stream
                }
                else
                {
                    break;
                }
            }

            return new Token(TokenType.Name, name);

        }
    }
}