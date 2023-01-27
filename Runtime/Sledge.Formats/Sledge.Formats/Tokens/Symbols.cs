// ReSharper disable MemberCanBePrivate.Global

namespace Sledge.Formats.Tokens
{
    public static class Symbols
    {
        ///<summary>!</summary>
        public const char Bang = '!';

        ///<summary>@</summary>
        public const char At = '@';

        ///<summary>#</summary>
        public const char Hash = '#';

        ///<summary>$</summary>
        public const char Dollar = '$';

        ///<summary>%</summary>
        public const char Percent = '%';

        ///<summary>^</summary>
        public const char Caret = '^';

        ///<summary>&amp;</summary>
        public const char Ampersand = '&';

        ///<summary>*</summary>
        public const char Star = '*';

        ///<summary>(</summary>
        public const char OpenParen = '(';

        ///<summary>)</summary>
        public const char CloseParen = ')';

        ///<summary>[</summary>
        public const char OpenBracket = '[';

        ///<summary>]</summary>
        public const char CloseBracket = ']';

        ///<summary>{</summary>
        public const char OpenBrace = '{';

        ///<summary>}</summary>
        public const char CloseBrace = '}';

        ///<summary>=</summary>
        public const char Equal = '=';

        ///<summary>+</summary>
        public const char Plus = '+';

        ///<summary>-</summary>
        public const char Minus = '-';

        ///<summary>_</summary>
        public const char Underscore = '_';

        ///<summary>/</summary>
        public const char Backslash = '/';

        ///<summary>|</summary>
        public const char Pipe = '|';

        ///<summary>:</summary>
        public const char Colon = ':';

        ///<summary>;</summary>
        public const char Semicolon = ';';

        ///<summary>"</summary>
        public const char DoubleQuote = '"';

        ///<summary>'</summary>
        public const char SingleQuote = '\'';

        ///<summary>,</summary>
        public const char Comma = ',';

        ///<summary>.</summary>
        public const char Dot = '.';

        ///<summary>/</summary>
        public const char Slash = '/';

        ///<summary><</summary>
        public const char Less = '<';

        ///<summary>></summary>
        public const char Greater = '>';

        ///<summary>?</summary>
        public const char Question = '?';

        // ReSharper disable once UnusedMember.Global
        public static char[] All =
        {
            Bang,
            At,
            Hash,
            Dollar,
            Percent,
            Caret,
            Ampersand,
            Star,
            OpenParen,
            CloseParen,
            OpenBracket,
            CloseBracket,
            OpenBrace,
            CloseBrace,
            Equal,
            Plus,
            Minus,
            Underscore,
            Backslash,
            Pipe,
            Colon,
            Semicolon,
            DoubleQuote,
            SingleQuote,
            Comma,
            Dot,
            Slash,
            Less,
            Greater,
            Question,
        };
    }
}