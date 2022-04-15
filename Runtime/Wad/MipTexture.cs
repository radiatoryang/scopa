using System;
using System.IO;
using System.Text;

namespace Scopa.Formats.Id
{
    public class MipTexture
    {
        public string Name { get; set; }
        public uint Width { get; set; }
        public uint Height { get; set; }
        public int NumMips { get; set; }
        public byte[][] MipData { get; set; }
        public byte[] Palette { get; set; }

        const int NameLength = 16;

        public static MipTexture Read(BinaryReader br, bool readPalette)
        {
            var position = br.BaseStream.Position;

            var texture = new MipTexture();

            var name = br.ReadChars(NameLength);
            var len = Array.IndexOf(name, '\0');
            texture.Name = new string(name, 0, len < 0 ? name.Length : len);

            texture.Width = br.ReadUInt32();
            texture.Height = br.ReadUInt32();
            var offsets = new[] { br.ReadUInt32(), br.ReadUInt32(), br.ReadUInt32(), br.ReadUInt32() };

            if (offsets[0] == 0)
            {
                texture.NumMips = 0;
                texture.MipData = new byte[0][];
                texture.Palette = readPalette ? QuakePalette.Data : new byte[0];
                return texture;
            }

            texture.NumMips = 4;
            texture.MipData = new byte[4][];

            int w = (int)texture.Width, h = (int)texture.Height;
            for (var i = 0; i < 4; i++) 
            {
                br.BaseStream.Seek(position + offsets[i], SeekOrigin.Begin);
                if ( i == 0)
                    texture.MipData[i] = br.ReadBytes(w * h);
                else
                    br.ReadBytes(w * h); // for Unity, we don't care about the other mip levels (instead, Unity generates the mip levels)
                w /= 2;
                h /= 2;
            }

            if (readPalette)
            {
                var paletteSize = br.ReadUInt16();
                texture.Palette = br.ReadBytes(paletteSize * 3);
            }
            else
            {
                texture.Palette = QuakePalette.Data;
            }

            return texture;
        }

        public static void Write(BinaryWriter bw, bool writePalette, MipTexture texture)
        {
            bw.WriteFixedLengthString(Encoding.ASCII, NameLength, texture.Name);
            bw.Write((uint) texture.Width);
            bw.Write((uint) texture.Height);

            if (texture.NumMips == 0)
            {
                bw.Write((uint) 0);
                bw.Write((uint) 0);
                bw.Write((uint) 0);
                bw.Write((uint) 0);
                bw.Write((ushort) 0);
                return;
            }

            uint currentOffset = NameLength + sizeof(uint) * 2 + sizeof(uint) * 4;

            for (var i = 0; i < 4; i++)
            {
                bw.Write((uint) currentOffset);
                currentOffset += (uint) texture.MipData[i].Length;
            }

            for (var i = 0; i < 4; i++)
            {
                bw.Write((byte[]) texture.MipData[i]);
            }

            if (writePalette)
            {
                bw.Write((ushort) (texture.Palette.Length / 3));
                bw.Write((byte[]) texture.Palette);
            }

            // need 2 bytes to pad at the end, or else TrenchBroom doesn't like it
            bw.Write(false);
            bw.Write(false);
        }
    }
}