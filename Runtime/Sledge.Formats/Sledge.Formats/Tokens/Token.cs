using System;
using System.Collections.Generic;

namespace Sledge.Formats.Tokens
{
    public class Token
    {
        public TokenType Type { get; }
        public string CustomType { get; }
        public string Value { get; }
        public int Line { get; set; }
        public int Column { get; set; }
        public List<string> Warnings { get; } = new List<string>();
        public List<Token> Leaders { get; } = new List<Token>();

        public char Symbol
        {
            get
            {
                if (Type != TokenType.Symbol || Value == null || Value.Length != 1) throw new ArgumentException($"Not a symbol: {Type}({Value})");
                return Value[0];
            }
        }

        public Token(TokenType type, string value = null)
        {
            Type = type;
            Value = value;
        }

        public Token(string customType, string value = null)
        {
            Type = TokenType.Custom;
            CustomType = customType;
            Value = value;
        }

        public bool Is(TokenType type, object value = null)
        {
            return type == Type && (value == null || value.ToString() == Value);
        }
    }
}
