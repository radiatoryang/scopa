using System.IO;

namespace Scopa.Formats.Texture.Wad.Lumps
{
    public class ImageLump : ILump
    {
        public LumpType Type => LumpType.Image;
        public int Width { get; set; }
        public int Height { get; set; }
        public byte[] ImageData { get; set; }

        public ImageLump(BinaryReader br)
        {
            Width = br.ReadInt32();
            Height = br.ReadInt32();
            ImageData = br.ReadBytes(Width * Height);
        }

        public int Write(BinaryWriter bw)
        {
            var pos = bw.BaseStream.Position;
            bw.Write((int) Width);
            bw.Write((int) Height);
            bw.Write((byte[]) ImageData);
            return (int)(bw.BaseStream.Position - pos);
        }
    }
}