using System.IO;

namespace Scopa.Formats.Texture.Wad.Lumps
{
    public interface ILump
    {
        LumpType Type { get; }
        int Write(BinaryWriter bw);
    }
}
