using System.IO;
using Scopa.Formats.Id;

namespace Scopa.Formats.Texture.Wad.Lumps
{
    public class MipTextureLump : RawTextureLump
    {
        public override LumpType Type => LumpType.MipTexture;
        public MipTexture texture;

        public MipTextureLump(BinaryReader br) : base(br, false)
        {
            // texture = Read(br, false);
        }

        public MipTextureLump() { }

        public override int Write(BinaryWriter bw)
        {
            var pos = bw.BaseStream.Position;
            Write(bw, true, this);
            return (int)(bw.BaseStream.Position - pos);
        }
    }
}