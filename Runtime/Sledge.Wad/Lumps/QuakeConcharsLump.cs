using System.IO;

namespace Scopa.Formats.Texture.Wad.Lumps
{
    public class QuakeConcharsLump : ILump
    {
        public LumpType Type => LumpType.RawTexture;
        public int Width { get; set; }
        public int Height { get; set; }
        public byte[] ImageData { get; set; }

        public QuakeConcharsLump(BinaryReader br)
        {
            Width = 128;
            Height = 128;
            ImageData = br.ReadBytes(Width * Height);
        }

        public int Write(BinaryWriter bw)
        {
            var pos = bw.BaseStream.Position;
            bw.Write((byte[]) ImageData);
            return (int)(bw.BaseStream.Position - pos);
        }
    }
}