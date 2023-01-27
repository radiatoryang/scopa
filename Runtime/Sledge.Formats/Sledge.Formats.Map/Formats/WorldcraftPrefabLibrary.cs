using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Sledge.Formats.Map.Objects;

namespace Sledge.Formats.Map.Formats
{
    public class WorldcraftPrefabLibrary
    {
        public List<Prefab> Prefabs { get; set; }

        public static WorldcraftPrefabLibrary FromFile(string file)
        {
            using (var stream = File.OpenRead(file))
            {
                return new WorldcraftPrefabLibrary(stream);
            }
        }

        public WorldcraftPrefabLibrary(Stream stream)
        {
            Prefabs = new List<Prefab>();
            using (var br = new BinaryReader(stream))
            {
                var header = br.ReadFixedLengthString(Encoding.ASCII, 28);
                var prefabLibraryHeader = "Worldcraft Prefab Library\r\n" + (char)0x1A;
                Util.Assert(header == prefabLibraryHeader, $"Incorrect prefab library header. Expected '{prefabLibraryHeader}', got '{header}'.");

                var version = br.ReadSingle();
                Util.Assert(Math.Abs(version - 0.1f) < 0.01, $"Unsupported prefab library version number. Expected 0.1, got {version}.");

                var rmf = new WorldcraftRmfFormat();

                var offset = br.ReadUInt32();
                var num = br.ReadUInt32();

                br.BaseStream.Seek(offset, SeekOrigin.Begin);
                for (var i = 0; i < num; i++)
                {
                    var objOffset = br.ReadUInt32();
                    var objLength = br.ReadUInt32();
                    var name = br.ReadFixedLengthString(Encoding.ASCII, 31);
                    var desc = br.ReadFixedLengthString(Encoding.ASCII, 205);
                    var _ = br.ReadBytes(300);

                    using (var substream = new SubStream(br.BaseStream, objOffset, objLength))
                    {

                        Prefabs.Add(new Prefab
                        {
                            Name = name,
                            Description = desc,
                            Map = rmf.Read(substream)
                        });
                    }
                }
            }
        }

        public void Save(string file)
        {
            using (var stream = File.OpenWrite(file))
            {
                Save(stream);
            }
        }

        public void Save(Stream stream)
        {
            throw new NotImplementedException("Writing prefab libraries is not currently supported.");
        }
    }
}
