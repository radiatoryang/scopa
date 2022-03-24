using System.Collections.Generic;
using System.IO;

namespace Scopa.Formats.Texture.Wad.Lumps
{
    public class FontLump : ILump
    {
        public LumpType Type => LumpType.Font;

        public int Width { get; set; }
        public int Height { get; set; }

        public int NumRows { get; set; }
        public int RowHeight { get; set; }

        public List<CharacterInfo> Characters { get; set; }

        public byte[] ImageData { get; set; }
        public byte[] Palette { get; set; }

        public FontLump(BinaryReader br)
        {
            Width = br.ReadInt32();
            Height = br.ReadInt32();

            NumRows = br.ReadInt32();
            RowHeight = br.ReadInt32();

            Characters = new List<CharacterInfo>();
            for (var i = 0; i < 256; i++)
            {
                Characters.Add(new CharacterInfo
                {
                    Offset = br.ReadInt16(),
                    Width = br.ReadInt16()
                });
            }

            ImageData = br.ReadBytes(Width * Height);
            var paletteSize = br.ReadUInt16();
            Palette = br.ReadBytes(paletteSize * 3);
        }

        public int Write(BinaryWriter bw)
        {
            var pos = bw.BaseStream.Position;

            bw.Write((int) Width);
            bw.Write((int) Height);

            bw.Write((int) NumRows);
            bw.Write((int) RowHeight);

            foreach (var ci in Characters)
            {
                bw.Write((short) ci.Offset);
                bw.Write((short) ci.Width);
            }

            bw.Write((byte[]) ImageData);
            bw.Write((ushort) (Palette.Length / 3));
            bw.Write((byte[]) Palette);

            return (int)(bw.BaseStream.Position - pos);
        }

        public class CharacterInfo
        {
            public short Offset { get; set; }
            public short Width { get; set; }
        }
    }
}