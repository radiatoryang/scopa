using System.IO;

namespace Scopa.Formats.Texture.Wad
{
    public class Header
    {
        public Version Version { get; set; }
        public int NumEntries { get; set; }
        public int DirectoryOffset { get; set; }

        public Header(Version version)
        {
            Version = version;
            NumEntries = 0;
            DirectoryOffset = 12;
        }

        public Header(BinaryReader br)
        {
            Version = (Version) br.ReadUInt32();
            NumEntries = br.ReadInt32();
            DirectoryOffset = br.ReadInt32();
        }

        public int Write(BinaryWriter bw)
        {
            var pos = bw.BaseStream.Position;
            bw.Write((uint) Version);
            bw.Write((int) NumEntries);
            bw.Write((int) DirectoryOffset);
            return (int)(bw.BaseStream.Position - pos);
        }
    }
}
