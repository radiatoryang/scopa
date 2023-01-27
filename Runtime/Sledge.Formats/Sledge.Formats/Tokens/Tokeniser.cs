using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Sledge.Formats.Tokens.Readers;

namespace Sledge.Formats.Tokens
{
    public class Tokeniser
    {
        public List<ITokenReader> Readers { get; }
        public bool AllowNewlinesInStrings
        {
            get => Readers.OfType<StringTokenReader>().FirstOrDefault()?.AllowNewlines == true;
            set => Readers.ForEach(x =>
            {
                if (x is StringTokenReader str) str.AllowNewlines = value;
            });
        }

        public bool EmitComments { get; set; } = false;
        public bool EmitWhitespace { get; set; } = false;

        private static List<ITokenReader> GetDefaultReaders(IEnumerable<char> symbols) => new List<ITokenReader>
        {
            new SingleLineCommentTokenReader(),
            new StringTokenReader(),
            new UnsignedIntegerTokenReader(),
            new SymbolTokenReader(symbols),
            new NameTokenReader(),
        };

        public Tokeniser(IEnumerable<char> symbols)
        {
            Readers = GetDefaultReaders(symbols);
        }

        public Tokeniser(params ITokenReader[] readers)
        {
            Readers = readers.ToList();
        }

        public IEnumerable<Token> Tokenise(string text)
        {
            using (var reader = new StringReader(text))
            {
                foreach (var t in Tokenise(reader)) yield return t;
            }
        }

        public IEnumerable<Token> Tokenise(TextReader input)
        {
            var reader = new CountingTextReader(input);
            int b;
            var leaders = new List<Token>();
            var currentWhitespace = "";
            var whitespaceStart = (-1, -1);
            while ((b = reader.Read()) >= 0)
            {
                if (b == '\r' || b == 0) continue;

                // Whitespace
                if (b == ' ' || b == '\t' || b == '\n')
                {
                    if (whitespaceStart.Item1 < 0) whitespaceStart = (reader.Line, reader.Column);
                    currentWhitespace += (char)b;
                    continue;
                }

                if (currentWhitespace.Length > 0)
                {
                    var ws = new Token(TokenType.Whitespace, currentWhitespace);
                    (ws.Line, ws.Column) = whitespaceStart;
                    if (EmitWhitespace) yield return ws;
                    else leaders.Add(ws);
                    currentWhitespace = "";
                    whitespaceStart = (-1, -1);
                }

                var token = ReadToken(b, reader, out var t) ? t : new Token(TokenType.Invalid, $"Unexpected token: {(char) b}");
                token.Line = reader.Line;
                token.Column = reader.Column;

                if (token.Type == TokenType.Comment && !EmitComments)
                {
                    leaders.Add(token);
                    continue;
                }

                token.Leaders.AddRange(leaders);
                leaders.Clear();

                yield return token;

                if (token.Type == TokenType.Invalid)
                {
                    yield break;
                }
            }

            // Put any trailing stuff in the end token
            if (currentWhitespace.Length > 0)
            {
                var ws = new Token(TokenType.Whitespace, currentWhitespace);
                (ws.Line, ws.Column) = whitespaceStart;
                if (EmitWhitespace) yield return ws;
                else leaders.Add(ws);
            }

            var end = new Token(TokenType.End);
            end.Leaders.AddRange(leaders);
            yield return end;
        }

        private bool ReadToken(int start, TextReader input, out Token token)
        {
            foreach (var reader in Readers)
            {
                token = reader.Read((char) start, input);
                if (token != null) return true;
            }

            token = null;
            return false;
        }

        private class CountingTextReader : TextReader
        {
            private readonly TextReader _reader;

            public int Line { get; set; } = 1;
            public int Column { get; set; } = 0;

            public CountingTextReader(TextReader reader)
            {
                _reader = reader;
            }

            public override int Read()
            {
                var num = _reader.Read();
                Column++;
                if (num == '\n') (Line, Column) = (Line + 1, 0);
                return num;
            }

            public override void Close() => _reader.Close();
            public override int Peek() => _reader.Peek();
            protected override void Dispose(bool disposing) => _reader.Dispose();
            public override object InitializeLifetimeService() => _reader.InitializeLifetimeService();

            // Technically these should count also, but meh
            public override int Read(char[] buffer, int index, int count) => _reader.Read(buffer, index, count);
            public override Task<int> ReadAsync(char[] buffer, int index, int count) => _reader.ReadAsync(buffer, index, count);
            public override int ReadBlock(char[] buffer, int index, int count) => _reader.ReadBlock(buffer, index, count);
            public override Task<int> ReadBlockAsync(char[] buffer, int index, int count) => _reader.ReadBlockAsync(buffer, index, count);
            public override string ReadLine() => _reader.ReadLine();
            public override Task<string> ReadLineAsync() => _reader.ReadLineAsync();
            public override string ReadToEnd() => _reader.ReadToEnd();
            public override Task<string> ReadToEndAsync() => _reader.ReadToEndAsync();
        }
    }
}