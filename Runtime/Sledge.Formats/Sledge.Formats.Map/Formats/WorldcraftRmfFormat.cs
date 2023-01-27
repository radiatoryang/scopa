using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using Sledge.Formats.Map.Objects;
using Path = Sledge.Formats.Map.Objects.Path;

// ReSharper disable UnusedParameter.Local - We pass through the version parameter even if we don't need it, for consistency
namespace Sledge.Formats.Map.Formats
{
    public class WorldcraftRmfFormat : IMapFormat
    {
        public string Name => "Worldcraft RMF";
        public string Description => "The .rmf file format used by Worldcraft and Valve Hammer Editor 3.";
        public string ApplicationName => "Worldcraft";
        public string Extension => "rmf";
        public string[] AdditionalExtensions => new[] { "rmx" };
        public string[] SupportedStyleHints => new[] { "1.6", "1.8", "2.2" };

        const int MaxVariableStringLength = 127;

        public MapFile Read(Stream stream)
        {
            using (var br = new BinaryReader(stream, Encoding.ASCII, true))
            {
                // Only RMF versions 1.8 and 2.2 are supported for the moment.
                var versionDouble = Math.Round(br.ReadSingle(), 1);
                Util.Assert(
                    Math.Abs(versionDouble - 2.2) < 0.01 ||
                    Math.Abs(versionDouble - 1.8) < 0.01 ||
                    Math.Abs(versionDouble - 1.6) < 0.01,
                    $"Unsupported RMF version number. Expected 1.8 or 2.2, got {versionDouble}.");
                var version = (RmfVersion)(int)Math.Round(versionDouble * 10);

                // RMF header test
                var header = br.ReadFixedLengthString(Encoding.ASCII, 3);
                Util.Assert(header == "RMF", $"Incorrect RMF header. Expected 'RMF', got '{header}'.");

                var map = new MapFile();

                ReadVisgroups(map, version, br);
                ReadWorldspawn(map, version, br);

                // Some RMF files might not have the DOCINFO block so we check if we're at the end of the stream
                if (stream.Position < stream.Length)
                {
                    // DOCINFO string check
                    var docinfo = br.ReadFixedLengthString(Encoding.ASCII, 8);
                    Util.Assert(docinfo == "DOCINFO", $"Incorrect RMF format. Expected 'DOCINFO', got '{docinfo}'.");

                    ReadCameras(map, version, br);
                }

                return map;
            }
        }

        #region Read

        private static void ReadVisgroups(MapFile map, RmfVersion version, BinaryReader br)
        {
            var numVisgroups = br.ReadInt32();
            for (var i = 0; i < numVisgroups; i++)
            {
                var vis = new Visgroup
                {
                    Name = br.ReadFixedLengthString(Encoding.ASCII, 128),
                    Color = br.ReadRGBAColour(),
                    ID = br.ReadInt32(),
                    Visible = br.ReadBoolean()
                };
                br.ReadBytes(3);
                map.Visgroups.Add(vis);
            }
        }

        private static void ReadWorldspawn(MapFile map, RmfVersion version, BinaryReader br)
        {
            var e = (Worldspawn) ReadObject(map, version, br);

            map.Worldspawn.SpawnFlags = e.SpawnFlags;
            foreach (var p in e.Properties) map.Worldspawn.Properties[p.Key] = p.Value;
            map.Worldspawn.Children.AddRange(e.Children);
        }

        private static MapObject ReadObject(MapFile map, RmfVersion version, BinaryReader br)
        {
            var type = br.ReadCString();
            switch (type)
            {
                case "CMapWorld":
                    return ReadRoot(map, version, br);
                case "CMapGroup":
                    return ReadGroup(map, version, br);
                case "CMapSolid":
                    return ReadSolid(map, version, br);
                case "CMapEntity":
                    return ReadEntity(map, version, br);
                default:
                    throw new ArgumentOutOfRangeException("Unknown RMF map object: " + type);
            }
        }

        private static void ReadMapBase(MapFile map, RmfVersion version, MapObject obj, BinaryReader br)
        {
            var visgroupId = br.ReadInt32();
            if (visgroupId > 0)
            {
                obj.Visgroups.Add(visgroupId);
            }

            obj.Color = br.ReadRGBColour();

            var numChildren = br.ReadInt32();
            for (var i = 0; i < numChildren; i++)
            {
                var child = ReadObject(map, version, br);
                if (child != null) obj.Children.Add(child);
            }
        }

        private static Worldspawn ReadRoot(MapFile map, RmfVersion version, BinaryReader br)
        {
            var wld = new Worldspawn();
            ReadMapBase(map, version, wld, br);
            ReadEntityData(version, wld, br);
            var numPaths = br.ReadInt32();
            for (var i = 0; i < numPaths; i++)
            {
                map.Paths.Add(ReadPath(version, br));
            }
            return wld;
        }

        private static Path ReadPath(RmfVersion version, BinaryReader br)
        {
            var path = new Path
            {
                Name = br.ReadFixedLengthString(Encoding.ASCII, 128),
                Type = br.ReadFixedLengthString(Encoding.ASCII, 128),
                Direction = (PathDirection) br.ReadInt32()
            };
            var numNodes = br.ReadInt32();
            for (var i = 0; i < numNodes; i++)
            {
                var node = new PathNode
                {
                    Position = br.ReadVector3(),
                    ID = br.ReadInt32(),
                    Name = br.ReadFixedLengthString(Encoding.ASCII, 128)
                };

                var numProps = br.ReadInt32();
                for (var j = 0; j < numProps; j++)
                {
                    var key = br.ReadCString();
                    var value = br.ReadCString();
                    node.Properties[key] = value;
                }
                path.Nodes.Add(node);
            }
            return path;
        }

        private static Group ReadGroup(MapFile map, RmfVersion version, BinaryReader br)
        {
            var grp = new Group();
            ReadMapBase(map, version, grp, br);
            return grp;
        }

        private static Solid ReadSolid(MapFile map, RmfVersion version, BinaryReader br)
        {
            var sol = new Solid();
            ReadMapBase(map, version, sol, br);
            var numFaces = br.ReadInt32();
            for (var i = 0; i < numFaces; i++)
            {
                var face = ReadFace(version, br);
                sol.Faces.Add(face);
            }
            return sol;
        }

        private static Entity ReadEntity(MapFile map, RmfVersion version, BinaryReader br)
        {
            var ent = new Entity();
            ReadMapBase(map, version, ent, br);
            ReadEntityData(version, ent, br);
            br.ReadBytes(2); // Unused
            var origin = br.ReadVector3();
            ent.Properties["origin"] = $"{origin.X.ToString("0.000", CultureInfo.InvariantCulture)} {origin.Y.ToString("0.000", CultureInfo.InvariantCulture)} {origin.Z.ToString("0.000", CultureInfo.InvariantCulture)}";
            br.ReadBytes(4); // Unused
            return ent;
        }

        private static void ReadEntityData(RmfVersion version, Entity e, BinaryReader br)
        {
            e.ClassName = br.ReadCString();
            br.ReadBytes(4); // Unused bytes
            e.SpawnFlags = br.ReadInt32();

            var numProperties = br.ReadInt32();
            for (var i = 0; i < numProperties; i++)
            {
                var key = br.ReadCString();
                var value = br.ReadCString();
                if (key == null) continue;
                e.Properties[key] = value;
            }

            br.ReadBytes(12); // More unused bytes
        }

        private static Face ReadFace(RmfVersion version, BinaryReader br)
        {
            /*
             * RMF version differences for faces:
             * 1.6: quake style - texname(40) , rotation, xshift, yshift, xscale, yscale, unused(4)
             * 1.8: quake style - texname(256), rotation, xshift, yshift, xscale, yscale, unused(16)
             * 2.2: valve style - texname(256), uaxis, xshift, vaxis, yshift, rotation, xscale, yscale, unused(16)
             */

            var face = new Face();
            if (version <= RmfVersion.Version16)
            {
                face.TextureName = br.ReadFixedLengthString(Encoding.ASCII, 40);
            }
            else
            {
                // Rather than 256 + 4 unused bytes, the texture name is likely 260 bytes as that is the maximum in Windows:
                // https://docs.microsoft.com/en-us/windows/win32/fileio/maximum-file-path-limitation?tabs=registry
                // In the Windows API, the maximum length for a path is MAX_PATH, which is defined as 260 characters.
                face.TextureName = br.ReadFixedLengthString(Encoding.ASCII, 260);
            }

            if (version <= RmfVersion.Version18)
            {
                // We need to use quake editor logic to work out the texture axes, for that we need the plane data - see below
                face.XShift = br.ReadSingle();
                face.YShift = br.ReadSingle();
                face.Rotation = br.ReadSingle();
            }
            else // if (version == RmfVersion.Version22)
            {
                face.UAxis = br.ReadVector3();
                face.XShift = br.ReadSingle();
                face.VAxis = br.ReadVector3();
                face.YShift = br.ReadSingle();
                face.Rotation = br.ReadSingle();
            }

            face.XScale = br.ReadSingle();
            face.YScale = br.ReadSingle();

            if (version <= RmfVersion.Version16)
            {
                br.ReadBytes(4); // Unused
            }
            else //if (version >= RmfVersion.Version18)
            {
                br.ReadBytes(16); // Unused
            }

            var numVerts = br.ReadInt32();
            for (var i = 0; i < numVerts; i++)
            {
                face.Vertices.Add(br.ReadVector3());
            }

            face.Plane = br.ReadPlane();

            if (version <= RmfVersion.Version18)
            {
                // Now work out what the texture axes are
                (face.UAxis, face.VAxis, _) = face.Plane.GetQuakeTextureAxes();
            }

            return face;
        }

        private static void ReadCameras(MapFile map, RmfVersion version, BinaryReader br)
        {
            br.ReadSingle(); // Appears to be a version number for camera data. Unused.
            var activeCamera = br.ReadInt32();

            var num = br.ReadInt32();
            for (var i = 0; i < num; i++)
            {
                map.Cameras.Add(new Camera
                {
                    EyePosition = br.ReadVector3(),
                    LookPosition = br.ReadVector3(),
                    IsActive = activeCamera == i
                });
            }
        }

        #endregion

        public void Write(Stream stream, MapFile map, string styleHint)
        {
            if (!String.IsNullOrWhiteSpace(styleHint) && styleHint != "2.2")
            {
                throw new NotImplementedException("Only saving v2.2 RMFs is supported.");
            }
            using (var bw = new BinaryWriter(stream, Encoding.ASCII, true))
            {
                // RMF 2.2 header
                bw.Write(2.2f);
                bw.WriteFixedLengthString(Encoding.ASCII, 3, "RMF");

                // Body
                WriteVisgroups(map, bw);
                WriteWorldspawn(map, bw);

                // Only write docinfo if there's cameras in the document
                if (map.Cameras.Any())
                {
                    // Docinfo footer
                    bw.WriteFixedLengthString(Encoding.ASCII, 8, "DOCINFO");
                    WriteCameras(map, bw);
                }
            }
        }

        #region Write

        private static void WriteVisgroups(MapFile map, BinaryWriter bw)
        {
            var vis = map.Visgroups;
            bw.Write(vis.Count);
            foreach (var visgroup in vis)
            {
                bw.WriteFixedLengthString(Encoding.ASCII, 128, visgroup.Name);
                bw.WriteRGBAColour(visgroup.Color);
                bw.Write(visgroup.ID);
                bw.Write(visgroup.Visible);
                bw.Write(new byte[3]); // Unused
            }
        }

        private static void WriteWorldspawn(MapFile map, BinaryWriter bw)
        {
            WriteObject(map.Worldspawn, bw);
            var paths = map.Paths;
            bw.Write(paths.Count);
            foreach (var path in paths)
            {
                WritePath(bw, path);
            }
        }

        private static void WriteObject(MapObject o, BinaryWriter bw)
        {
            switch (o)
            {
                case Worldspawn r:
                    WriteRoot(r, bw);
                    break;
                case Group g:
                    WriteGroup(g, bw);
                    break;
                case Solid s:
                    WriteSolid(s, bw);
                    break;
                case Entity e:
                    WriteEntity(e, bw);
                    break;
                default:
                    throw new ArgumentOutOfRangeException("Unsupported RMF map object: " + o.GetType());
            }
        }

        private static void WriteMapBase(MapObject obj, BinaryWriter bw)
        {
            bw.Write(obj.Visgroups.Any() ? obj.Visgroups[0] : 0);
            bw.WriteRGBColour(obj.Color);
            bw.Write(obj.Children.Count);
            foreach (var child in obj.Children)
            {
                WriteObject(child, bw);
            }
        }

        private static void WriteRoot(Worldspawn root, BinaryWriter bw)
        {
            bw.WriteCString("CMapWorld", MaxVariableStringLength);
            WriteMapBase(root, bw);
            WriteEntityData(root, bw);
        }

        private static void WritePath(BinaryWriter bw, Path path)
        {
            bw.WriteFixedLengthString(Encoding.ASCII, 128, path.Name);
            bw.WriteFixedLengthString(Encoding.ASCII, 128, path.Type);
            bw.Write((int)path.Direction);
            bw.Write(path.Nodes.Count);
            foreach (var node in path.Nodes)
            {
                bw.WriteVector3(node.Position);
                bw.Write(node.ID);
                bw.WriteFixedLengthString(Encoding.ASCII, 128, node.Name);
                bw.Write(node.Properties.Count);
                foreach (var property in node.Properties)
                {
                    bw.WriteCString(property.Key, MaxVariableStringLength);
                    bw.WriteCString(property.Value, MaxVariableStringLength);
                }
            }
        }

        private static void WriteGroup(Group group, BinaryWriter bw)
        {
            bw.WriteCString("CMapGroup", MaxVariableStringLength);
            WriteMapBase(group, bw);
        }

        private static void WriteSolid(Solid solid, BinaryWriter bw)
        {
            bw.WriteCString("CMapSolid", MaxVariableStringLength);
            WriteMapBase(solid, bw);
            var faces = solid.Faces.ToList();
            bw.Write(faces.Count);
            foreach (var face in faces)
            {
                WriteFace(face, bw);
            }
        }

        private static void WriteEntity(Entity entity, BinaryWriter bw)
        {
            bw.WriteCString("CMapEntity", MaxVariableStringLength);
            WriteMapBase(entity, bw);
            WriteEntityData(entity, bw);
            bw.Write(new byte[2]); // Unused

            var origin = new Vector3();
            if (entity.Properties.ContainsKey("origin"))
            {
                var o = entity.Properties["origin"];
                if (!String.IsNullOrWhiteSpace(o))
                {
                    var parts = o.Split(' ');
                    if (parts.Length == 3)
                    {
                        NumericsExtensions.TryParse(parts[0], parts[1], parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out origin);
                    }
                }
            }
            bw.WriteVector3(origin);
            bw.Write(new byte[4]); // Unused
        }

        private static void WriteEntityData(Entity data, BinaryWriter bw)
        {
            bw.WriteCString(data.ClassName, MaxVariableStringLength);
            bw.Write(new byte[4]); // Unused
            bw.Write(data.SpawnFlags);

            var props = data.Properties.Where(x => !String.IsNullOrWhiteSpace(x.Key)).ToList();
            bw.Write(props.Count);
            foreach (var p in props)
            {
                bw.WriteCString(p.Key, MaxVariableStringLength);
                bw.WriteCString(p.Value, MaxVariableStringLength);
            }
            bw.Write(new byte[12]); // Unused
        }

        private static void WriteFace(Face face, BinaryWriter bw)
        {
            bw.WriteFixedLengthString(Encoding.ASCII, 256, face.TextureName);
            bw.Write(new byte[4]);
            bw.WriteVector3(face.UAxis);
            bw.Write(face.XShift);
            bw.WriteVector3(face.VAxis);
            bw.Write(face.YShift);
            bw.Write(face.Rotation);
            bw.Write(face.XScale);
            bw.Write(face.YScale);
            bw.Write(new byte[16]);
            bw.Write(face.Vertices.Count);
            foreach (var vertex in face.Vertices)
            {
                bw.WriteVector3(vertex);
            }
            bw.WritePlane(face.Vertices.ToArray());
        }

        private static void WriteCameras(MapFile map, BinaryWriter bw)
        {
            bw.Write(0.2f); // Unused

            var cams = map.Cameras;
            var active = Math.Max(0, cams.FindIndex(x => x.IsActive));

            bw.Write(active);
            bw.Write(cams.Count);
            foreach (var cam in cams)
            {
                bw.WriteVector3(cam.EyePosition);
                bw.WriteVector3(cam.LookPosition);
            }
        }

        #endregion

        private enum RmfVersion
        {
            // Currently unsupported, but known, RMF versions
            /*
            /// <summary>
            /// RMF Version 0.8, used by Worldcraft 1.0a - 1.1
            /// </summary>
            Version08 = 8,

            /// <summary>
            /// RMF Version 0.9, used by Worldcraft 1.1a
            /// </summary>
            Version09 = 9,

            /// <summary>
            /// RMF Version 1.4, used by Worldcraft 1.3
            /// </summary>
            Version14 = 14,
            */

            /// <summary>
            /// RMF Version 1.6, used by Worldcraft 1.5b
            /// </summary>
            Version16 = 16,

            /// <summary>
            /// RMF Version 1.8, used by Worldcraft 1.6 - 2.1
            /// </summary>
            Version18 = 18,

            /// <summary>
            /// RMF Version 2.2, used by Worldcraft 3.3 and Valve Hammer Editor 3.4 - 3.5
            /// </summary>
            // ReSharper disable once UnusedMember.Local
            Version22 = 22,
        }
    }
}
