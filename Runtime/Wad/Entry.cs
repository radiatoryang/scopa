using System.IO;
using System.Text;

namespace Scopa.Formats.Texture.Wad
{
    public class Entry
    {
        public const int NameLength = 16;

        public int Offset { get; set; }
        public int CompressedSize { get; set; }
        public int UncompressedSize { get; set; }
        public LumpType Type { get; set; }
        public bool Compression { get; set; }
        public string Name { get; set; }

        public Entry(string name, LumpType lumpType)
        {
            Name = name;
            Type = lumpType;
        }

        public Entry(BinaryReader br)
        {
            Offset = br.ReadInt32();
            CompressedSize = br.ReadInt32();
            UncompressedSize = br.ReadInt32();
            Type = (LumpType) br.ReadByte();
            Compression = br.ReadByte() != 0;
            br.ReadBytes(2);
            Name = br.ReadFixedLengthString(Encoding.ASCII, NameLength);
        }

        public int Write(BinaryWriter bw)
        {
            var pos = bw.BaseStream.Position;
            bw.Write((int) Offset);
            bw.Write((int) CompressedSize);
            bw.Write((int) UncompressedSize);
            bw.Write((byte) Type);
            bw.Write((byte) (Compression ? 1 : 0));
            bw.Write((short) 2);
            bw.WriteFixedLengthString(Encoding.ASCII, NameLength, Name);
            return (int)(bw.BaseStream.Position - pos);
        }
    }
}