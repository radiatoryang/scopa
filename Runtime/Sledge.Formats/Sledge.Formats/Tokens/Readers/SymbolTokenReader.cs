using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Sledge.Formats.Tokens.Readers
{
    /// <summary>
    /// Reads a symbol as a single character from a list of valid symbols.
    /// </summary>
    public class SymbolTokenReader : ITokenReader
    {
        private readonly HashSet<int> _symbolSet;

        public SymbolTokenReader(IEnumerable<char> symbols)
        {
            _symbolSet = new HashSet<int>(symbols.Select(x => (int)x));
        }

        public Token Read(char start, TextReader reader)
        {
            return _symbolSet.Contains(start) ? new Token(TokenType.Symbol, start.ToString()) : null;
        }
    }
}