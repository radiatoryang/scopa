using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Sledge.Formats.Tokens
{
    public static class TokenParsing
    {
        /// <summary>
        /// Expect the current token to match a particular type and optionally a value, and then move to the next token
        /// </summary>
        public static Token Expect(IEnumerator<Token> it, TokenType type, object value)
        {
            return Expect(it, type, x => x == value.ToString());
        }

        /// <summary>
        /// Expect the current token to match a particular type and optionally a value, and then move to the next token
        /// </summary>
        public static Token Expect(IEnumerator<Token> it, TokenType type, Predicate<string> valueChecker = null)
        {
            var tok = it.Current;

            if (tok == null) throw new Exception($"Parsing error (line {tok.Line}, column {tok.Column}): Expected {type}, got no token");
            if (tok.Type != type) throw new Exception($"Parsing error (line {tok.Line}, column {tok.Column}): Expected {type}, got {tok.Type}({tok.Value})");
            if (valueChecker != null && !valueChecker(tok.Value)) throw new Exception($"Parsing error (line {tok.Line}, column {tok.Column}): Unexpected token value {tok.Type}({tok.Value})");

            it.MoveNext();
            return tok;
        }

        public static List<Token> BalanceBrackets(IEnumerator<Token> it, char open, char close)
        {
            var tokens = new List<Token>();

            var tok = it.Current;
            if (tok == null || !tok.Is(TokenType.Symbol, open)) return tokens;
            var startToken = tok;
            
            var stack = new Stack<Token>();

            do
            {
                tok = it.Current ?? throw new Exception($"Parsing error (line {tok.Line}, column {tok.Column}): Unexpected end of token stream");
                if (tok.Is(TokenType.Symbol, open)) stack.Push(tok);
                else if (tok.Is(TokenType.Symbol, close)) stack.Pop();
                tokens.Add(tok);
                if (!it.MoveNext()) throw new Exception($"Parsing error (line {tok.Line}, column {tok.Column}): Unexpected end of file - start token at {startToken.Type}({startToken.Value}) (line {startToken.Line}, column {startToken.Column})");
            } while (stack.Count > 0);

            return tokens;
        }

        public static string ParseAppendedString(IEnumerator<Token> it)
        {
            if (it.Current?.Type != TokenType.String) return "";

            var str = Expect(it, TokenType.String).Value;

            while (it.Current?.Is(TokenType.Symbol, Symbols.Plus) == true)
            {
                if (!it.MoveNext() || it.Current == null || !it.Current.Is(TokenType.String)) break;
                str += it.Current.Value;
                if (!it.MoveNext()) break;
            }

            return str;
        }

        /// <summary>
        /// Starting at the current token, parse an integer with an optional leading sign
        /// </summary>
        public static int ParseInteger(IEnumerator<Token> tokens)
        {
            var neg = false;
            var valid = false;
            var value = 0;
            
            var src = tokens.Current;
            Debug.Assert(src != null, nameof(src) + " != null");

            var tok = src;

            // If there's a sign it must be the first symbol, check for it first
            if (tok.Type == TokenType.Symbol)
            {
                if (tok.Symbol == Symbols.Minus || tok.Symbol == Symbols.Plus)
                {
                    neg = tok.Symbol == Symbols.Minus;
                    tokens.MoveNext();
                }
            }

            tok = tokens.Current;
            if (tok == null) throw new Exception($"Parsing error (line {src.Line}, column {src.Column}): Unexpected end of stream");

            // If we have a number, it's the left side of the decimal
            if (tok.Type == TokenType.Number)
            {
                value = int.Parse(tok.Value);
                valid = true;
                
                tokens.MoveNext();
                tok = tokens.Current;
                if (tok == null) throw new Exception($"Parsing error (line {src.Line}, column {src.Column}): Unexpected end of stream");
            }

            if (!valid) throw new Exception($"Parsing error (line {src.Line}, column {src.Column}): Unable to parse number value");

            if (neg) value *= -1;
            return value;
        }
        
        /// <summary>
        /// Starting at the current token, parse a decimal with an optional leading sign and decimal place
        /// </summary>
        public static decimal ParseDecimal(IEnumerator<Token> tokens)
        {
            var neg = false;
            var valid = false;
            var value = 0m;
            
            var src = tokens.Current;
            Debug.Assert(src != null, nameof(src) + " != null");

            var tok = src;

            // If there's a sign it must be the first symbol, check for it first
            if (tok.Type == TokenType.Symbol)
            {
                if (tok.Symbol == Symbols.Minus || tok.Symbol == Symbols.Plus)
                {
                    neg = tok.Symbol == Symbols.Minus;
                    tokens.MoveNext();
                }
            }

            tok = tokens.Current;
            if (tok == null) throw new Exception($"Parsing error (line {src.Line}, column {src.Column}): Unexpected end of stream");

            // If we have a number, it's the left side of the decimal
            if (tok.Type == TokenType.Number)
            {
                value = int.Parse(tok.Value);
                valid = true;
                
                tokens.MoveNext();
                tok = tokens.Current;
                if (tok == null) throw new Exception($"Parsing error (line {src.Line}, column {src.Column}): Unexpected end of stream");
            }

            // If we have a decimal, we want to read the next number (if it exists) too
            if (tok.Type == TokenType.Symbol && tok.Symbol == Symbols.Dot)
            {
                tokens.MoveNext();
                tok = tokens.Current;
                if (tok == null) throw new Exception($"Parsing error (line {src.Line}, column {src.Column}): Unexpected end of stream");

                if (tok.Type == TokenType.Number)
                {
                    var dec = int.Parse(tok.Value);
                    var pow = (decimal) Math.Pow(10, -tok.Value.Length);
                    value += pow * dec;
                    valid = true;

                    tokens.MoveNext();
                    tok = tokens.Current;
                    if (tok == null) throw new Exception($"Parsing error (line {src.Line}, column {src.Column}): Unexpected end of stream");
                }
            }

            // If we have 'e' followed by an integer, assume we have an exponent
            if (tok.Type == TokenType.Name && tok.Value.StartsWith("e") && int.TryParse(tok.Value.Substring(1), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var exponent))
            {
                value *= (decimal) Math.Pow(10, exponent);
                tokens.MoveNext();
                tok = tokens.Current;
            }

            if (!valid) throw new Exception($"Parsing error (line {src.Line}, column {src.Column}): Unable to parse number value");

            if (neg) value *= -1;
            return value;
        }

        public static void SkipTrivia(IEnumerator<Token> tokens, bool whitespace = true, bool comments = true)
        {
            SkipWhile(tokens, tok => (whitespace && tok.Type == TokenType.Whitespace) || (comments && tok.Type == TokenType.Comment));
        }

        public static void SkipWhile(IEnumerator<Token> tokens, Predicate<Token> test)
        {
            while (true)
            {
                var cur = tokens.Current;
                if (cur == null) break;
                if (test(cur)) tokens.MoveNext();
                else break;
            }
        }
    }
}
