using System.IO;
using UnityEngine;

namespace Scopa.Formats.Texture.Wad.Lumps
{
    public class RawTextureLump : Id.MipTexture, ILump
    {
        public virtual LumpType Type => LumpType.RawTexture;

        public RawTextureLump() {}

        public RawTextureLump(BinaryReader br) : this(br, false)
        {
            //
        }

        public RawTextureLump(BinaryReader br, bool readPalette)
        {
            var t = Read(br, readPalette);
            Name = t.Name;
            Width = t.Width;
            Height = t.Height;
            NumMips = t.NumMips;
            MipData = t.MipData;
            Palette = t.Palette;
        }

        public virtual int Write(BinaryWriter bw)
        {
            var pos = bw.BaseStream.Position;
            Write(bw, false, this);
            return (int) (bw.BaseStream.Position - pos);
        }
    }
}