using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Scopa.Formats.Texture.Wad.Lumps;
using UnityEngine;

namespace Scopa.Formats.Texture.Wad
{
    public class WadFile
    {
        public string Name { get; set; }
        public Header Header { get; set; }
        public List<Entry> Entries { get; set; }

        private Dictionary<Entry, ILump> _lumps;
        public IEnumerable<ILump> Lumps => _lumps.Values;

        /// <summary>
        /// Create a blank wad file.
        /// </summary>
        public WadFile(Version version)
        {
            Header = new Header(version);
            Entries = new List<Entry>();
            _lumps = new Dictionary<Entry, ILump>();
        }

        /// <summary>
        /// Load a wad directory from a stream, and optionally load all lumps into memory.
        /// </summary>
        /// <param name="stream">The stream to load data from</param>
        /// <param name="loadLumps">True to load all lumps into memory, false to load the directory only</param>
        public WadFile(Stream stream, bool loadLumps = true)
        {
            Entries = new List<Entry>();
            using (var br = new BinaryReader(stream, Encoding.ASCII, true))
            {
                Header = new Header(br);

                if (Header.Version != Version.Wad2 && Header.Version != Version.Wad3)
                {
                    throw new NotSupportedException("Only idTech2 and Goldsource (WAD2 & WAD3) WAD files are supported.");
                }

                br.BaseStream.Seek(Header.DirectoryOffset, SeekOrigin.Begin);
                for (var i = 0; i < Header.NumEntries; i++)
                {
                    var entry = new Entry(br);
                    Entries.Add(entry);
                }
            }

            if (loadLumps)
            {
                LoadLumps(Header.Version, stream);
            }
        }

        /// <summary>
        /// Write the file to a stream. The lumps must be loaded into memory for this to work.
        /// </summary>
        /// <param name="stream">The stream to write to</param>
        public int Write(Stream stream)
        {
            using (var bw = new BinaryWriter(stream, Encoding.ASCII, true))
            {
                var entries = new List<Entry>();
                var startPos = bw.BaseStream.Position;

                Header.Write(bw); // We'll come back to this later

                // Write the lumps
                foreach (var kv in _lumps)
                {
                    var pos = bw.BaseStream.Position - startPos;
                    var size = kv.Value.Write(bw);
                    var e = kv.Key;
                    e.Offset = (int) pos;
                    e.UncompressedSize = e.CompressedSize = size;
                    entries.Add(e);
                }

                // Write the entries
                Header.NumEntries = entries.Count;
                Header.DirectoryOffset = (int) (bw.BaseStream.Position - startPos);
                foreach (var e in entries) e.Write(bw);

                // Re-write the header
                var endPos = bw.BaseStream.Position;
                bw.BaseStream.Position = startPos;
                Header.Write(bw);
                bw.BaseStream.Position = endPos;

                return (int) (endPos - startPos);
            }
        }

        /// <summary>
        /// Load all lumps into memory. This is done already if <code>loadLumps</code> is true in the constructor.
        /// </summary>
        /// <param name="stream">The stream to load from</param>
        public void LoadLumps(Version version, Stream stream)
        {
            _lumps = new Dictionary<Entry, ILump>();
            foreach (var entry in Entries.OrderBy(x => x.Offset))
            {
                var lump = LoadLump(version, stream, entry);
                _lumps.Add(entry, lump);
            }
        }

        /// <summary>
        /// Get a lump from memory. The lumps must be loaded into memory for this method to work.
        /// </summary>
        /// <param name="entry">The entry to get</param>
        /// <returns>The lump, if it exists</returns>
        public ILump GetLump(Entry entry)
        {
            return _lumps.TryGetValue(entry, out var l) ? l : null;
        }

        /// <summary>
        /// Load a lump from a given stream. This is not required if <code>loadLumps</code> is true in the constructor.
        /// This will not load this lump into the Lumps list, that can only be done with <code>LoadLumps</code>.
        /// </summary>
        /// <param name="stream">The stream to load data from</param>
        /// <param name="entry">The entry to load</param>
        /// <returns>The loaded lump.</returns>
        public ILump LoadLump(Version version, Stream stream, Entry entry)
        {
            using (var br = new BinaryReader(stream, Encoding.ASCII, true))
            {
                br.BaseStream.Seek(entry.Offset, SeekOrigin.Begin);

                // Quake's gfx.wad has a hacked conchars lump
                if (entry.CompressedSize == 16384 && entry.Name.Equals("conchars", StringComparison.CurrentCultureIgnoreCase))
                {
                    return new QuakeConcharsLump(br);
                }

                ILump lump;
                switch (entry.Type)
                {
                    case LumpType.Palette:
                        lump = new PaletteLump(br);
                        break;
                    case LumpType.ColorMap:
                        lump = new ColorMapLump(br);
                        break;
                    case LumpType.Image:
                        lump = new ImageLump(br);
                        break;
                    case LumpType.MipTexture:
                        lump = new RawTextureLump(br, version == Version.Wad3);
                        break;
                    case LumpType.RawTexture:
                        lump = new RawTextureLump(br, version == Version.Wad3);
                        break;
                    case LumpType.ColorMap2:
                        lump = new ColorMap2Lump(br);
                        break;
                    case LumpType.Font:
                        lump = new FontLump(br);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                return lump;
            }
        }

        /// <summary>
        /// Add a lump to the set. The lumps must be loaded into memory for this to work.
        /// </summary>
        /// <param name="name">The name of the entry</param>
        /// <param name="lump">The lump to add</param>
        /// <returns>The entry of the lump that was added</returns>
        public Entry AddLump(string name, ILump lump)
        {
            var e = new Entry(name, lump.Type);
            _lumps[e] = lump;
            return e;
        }
    }
}