using System;
using System.IO;

namespace Sledge.Formats.Tokens.Readers
{
    /// <summary>
    /// Reads a number consisting of a sign, integer component, decimal, decimal component, and exponent.
    /// If sign is included, the integer component is mandatory.
    /// If the decimal is included, the decimal component is mandatory.
    /// If the exponent is included, either the integer or decimal components are mandatory.
    /// </summary>
    public class NumberTokenReader : ITokenReader
    {
        public bool AllowSign { get; set; } = true;
        public bool AllowExponent { get; set; } = true;

        public Token Read(char start, TextReader reader)
        {
            throw new NotImplementedException();
        }
    }
}