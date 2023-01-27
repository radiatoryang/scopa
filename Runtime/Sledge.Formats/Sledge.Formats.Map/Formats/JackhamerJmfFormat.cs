using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using Sledge.Formats.Map.Objects;
using Path = Sledge.Formats.Map.Objects.Path;

namespace Sledge.Formats.Map.Formats
{
    // Some code borrowed from https://github.com/pwitvoet/mess/blob/master/MESS/Formats/JmfFormat.cs
    public class JackhammerJmfFormat : IMapFormat
    {
        public string Name => "Jackhammer JMF";
        public string Description => "The .jmf file format used by Jackhammer and JACK.";
        public string ApplicationName => "JACK";
        public string Extension => "jmf";
        public string[] AdditionalExtensions => new[] { "jmx" };
        public string[] SupportedStyleHints => new[] { "" };

        public MapFile Read(Stream stream)
        {
            using (var br = new BinaryReader(stream, Encoding.ASCII, true))
            {
                // JMF header test
                var header = br.ReadFixedLengthString(Encoding.ASCII, 4);
                Util.Assert(header == "JHMF", $"Incorrect JMF header. Expected 'JHMF', got '{header}'.");

                // Only JHMF version 121 is supported for the moment.
                var version = br.ReadInt32();
                Util.Assert(version == 121, $"Unsupported JMF version number. Expected 121, got {version}.");

                // Appears to be an array of locations to export to .map
                var numExportStrings = br.ReadInt32();
                for (var i = 0; i < numExportStrings; i++)
                {
                    ReadString(br);
                }

                var map = new MapFile();

                var groups = ReadGroups(map, br);
                ReadVisgroups(map, br);
                map.CordonBounds = (br.ReadVector3(), br.ReadVector3());
                ReadCameras(map, br);
                ReadPaths(map, br);
                var entities = ReadEntities(map, br);

                BuildTree(map, groups, entities);

                return map;
            }
        }

        #region Read

        private void BuildTree(MapFile map, IEnumerable<JmfGroup> groups, IReadOnlyCollection<JmfEntity> entities)
        {
            var groupIds = new Dictionary<int, int>(); // file group id -> actual id
            var objTree = new Dictionary<int, MapObject>(); // object id -> object
            
            var currentId = 2; // worldspawn is 1
            objTree[1] = map.Worldspawn;

            var worldspawnEntity = entities.FirstOrDefault(x => x.Entity.ClassName == "worldspawn");
            if (worldspawnEntity != null)
            {
                map.Worldspawn.Properties = worldspawnEntity.Entity.Properties;
                map.Worldspawn.Color = worldspawnEntity.Entity.Color;
                map.Worldspawn.SpawnFlags = worldspawnEntity.Entity.SpawnFlags;
                map.Worldspawn.Visgroups = worldspawnEntity.Entity.Visgroups;
            }
            
            // Jackhammer doesn't allow a group within an entity, so groups
            // will only be children of worldspawn or another group. We can
            // build the group hierarchy immediately.
            var groupList = groups.ToList();
            var groupCount = groupList.Count;
            while (groupList.Any())
            {
                var pcs = groupList.Where(x => x.ID == x.ParentID || x.ParentID == 0 || groupIds.ContainsKey(x.ParentID)).ToList();
                foreach (var g in pcs)
                {
                    var gid = currentId++;
                    groupIds[g.ID] = gid;
                    groupList.Remove(g);

                    var group = new Group
                    {
                        Color = g.Color
                    };

                    var parentObjId = g.ID == g.ParentID || g.ParentID == 0 ? 1 : groupIds[g.ParentID];
                    objTree[parentObjId].Children.Add(group);
                    objTree[gid] = group;
                }

                if (groupList.Count == groupCount) break; // no groups processed, can't continue
                groupCount = groupList.Count;
            }

            // For non-worldspawn solids, they are direct children of their entity.
            // For non-worldspawn entities, they're either a child of a group or of the worldspawn.
            foreach (var entity in entities.Where(x => x != worldspawnEntity))
            {
                var parentId = groupIds.ContainsKey(entity.GroupID) ? groupIds[entity.GroupID] : 1;
                objTree[parentId].Children.Add(entity.Entity);
                
                // Put all the entity's solids straight underneath this entity
                entity.Entity.Children.AddRange(entity.Solids.Select(x => x.Solid));
            }

            // For worldspawn solids, they're either a child of a group or of the worldspawn.
            if (worldspawnEntity != null)
            {
                foreach (var solid in worldspawnEntity.Solids)
                {
                    var parentId = groupIds.ContainsKey(solid.GroupID) ? groupIds[solid.GroupID] : 1;
                    objTree[parentId].Children.Add(solid.Solid);
                }
            }
        }

        private List<JmfGroup> ReadGroups(MapFile map, BinaryReader br)
        {
            var groups = new List<JmfGroup>();

            var numGroups = br.ReadInt32();
            for (var i = 0; i < numGroups; i++)
            {
                var g = new JmfGroup
                {
                    ID = br.ReadInt32(),
                    ParentID = br.ReadInt32(),
                    Flags = br.ReadInt32(),
                    NumObjects = br.ReadInt32(),
                    Color = br.ReadRGBAColour()
                };
                groups.Add(g);
            }

            return groups;
        }

        private static void ReadVisgroups(MapFile map, BinaryReader br)
        {
            var numVisgroups = br.ReadInt32();
            for (var i = 0; i < numVisgroups; i++)
            {
                var vis = new Visgroup
                {
                    Name = ReadString(br),
                    ID = br.ReadInt32(),
                    Color = br.ReadRGBAColour(),
                    Visible = br.ReadBoolean()
                };
                map.Visgroups.Add(vis);
            }
        }

        private void ReadCameras(MapFile map, BinaryReader br)
        {
            var numCameras = br.ReadInt32();
            for (var i = 0; i < numCameras; i++)
            {
                var cam = new Camera
                {
                    EyePosition = br.ReadVector3(),
                    LookPosition = br.ReadVector3(),
                    IsActive = ((JmfFlags) br.ReadInt32()).HasFlag(JmfFlags.Selected),
                    Color = br.ReadRGBAColour()
                };
                map.Cameras.Add(cam);
            }
        }

        private void ReadPaths(MapFile map, BinaryReader br)
        {
            var numPaths = br.ReadInt32();
            for (var i = 0; i < numPaths; i++)
            {
                map.Paths.Add(ReadPath(br));
            }
        }

        private static Path ReadPath(BinaryReader br)
        {
            var path = new Path
            {
                Type = ReadString(br),
                Name = ReadString(br),
                Direction = (PathDirection) br.ReadInt32(),
                Flags = br.ReadInt32(),
                Color = br.ReadRGBAColour()
            };

            var numNodes = br.ReadInt32();
            for (var i = 0; i < numNodes; i++)
            {
                var name = ReadString(br);
                var fire = ReadString(br); // fire on pass
                var node = new PathNode
                {
                    Name = name,
                    Position = br.ReadVector3()
                };

                if (!String.IsNullOrWhiteSpace(fire)) node.Properties["message"] = fire;

                var angles = br.ReadVector3();
                node.Properties["angles"] = $"{angles.X} {angles.Y} {angles.Z}";

                node.Properties["spawnflags"] = br.ReadInt32().ToString();

                node.Color = br.ReadRGBAColour();

                var numProps = br.ReadInt32();
                for (var j = 0; j < numProps; j++)
                {
                    var key = ReadString(br);
                    var value = ReadString(br);
                    if (key != null && value != null) node.Properties[key] = value;
                }

                path.Nodes.Add(node);
            }
            return path;
        }

        private List<JmfEntity> ReadEntities(MapFile map, BinaryReader br)
        {
            var entities = new List<JmfEntity>();
            while (br.BaseStream.Position < br.BaseStream.Length)
            {
                var ent = new JmfEntity
                {
                    Entity = new Entity
                    {
                        ClassName = ReadString(br)
                    }
                };

                var origin = br.ReadVector3();
                ent.Entity.Properties["origin"] = $"{origin.X} {origin.Y} {origin.Z}";

                ent.Flags = br.ReadInt32();
                ent.GroupID = br.ReadInt32();
                var rootGroupId = br.ReadInt32(); // We don't care about the root group id as we are assembling the tree later
                ent.Entity.Color = br.ReadRGBAColour();
                
                // useless (?) list of 13 strings
                for (var i = 0; i < 13; i++) ReadString(br);

                ent.Entity.SpawnFlags = br.ReadInt32();

                // Some special properties, but these are duplicated in the keyvalues, so we don't care about them
                var angles = br.ReadVector3();
                var rendering = (JmfRendering) br.ReadInt32();
                var fxColor = br.ReadRGBAColour();
                var renderMode = br.ReadInt32();
                var renderFx = br.ReadInt32();
                var body = br.ReadInt16();
                var skin = br.ReadInt16();
                var sequence = br.ReadInt32();
                var framerate = br.ReadSingle();
                var scale = br.ReadSingle();
                var radius = br.ReadSingle();

                br.ReadBytes(28); // unknown - these bytes are all 0 in a bunch of different files I tried

                var numProps = br.ReadInt32();
                for (var i = 0; i < numProps; i++)
                {
                    var key = ReadString(br);
                    var value = ReadString(br);
                    if (key != null && value != null) ent.Entity.Properties[key] = value;
                }

                ent.Entity.Visgroups = new List<int>();

                var numVisgroups = br.ReadInt32();
                for (var i = 0; i < numVisgroups; i++)
                {
                    ent.Entity.Visgroups.Add(br.ReadInt32());
                }

                var numSolids = br.ReadInt32();
                for (var i = 0; i < numSolids; i++)
                {
                    ent.Solids.Add(ReadSolid(map, br));
                }

                entities.Add(ent);
            }

            return entities;
        }

        private JmfSolid ReadSolid(MapFile map, BinaryReader br)
        {
            var solid = new JmfSolid
            {
                Solid = new Solid()
            };

            var numPatches = br.ReadInt32();
            solid.Flags = br.ReadInt32();
            solid.GroupID = br.ReadInt32();
            var rootGroupId = br.ReadInt32(); // We don't care about the root group id as we are assembling the tree later
            solid.Solid.Color = br.ReadRGBAColour();
            
            var numVisgroups = br.ReadInt32();
            for (var i = 0; i < numVisgroups; i++)
            {
                solid.Solid.Visgroups.Add(br.ReadInt32());
            }

            var numFaces = br.ReadInt32();
            for (var i = 0; i < numFaces; i++)
            {
                solid.Solid.Faces.Add(ReadFace(br));
            }

            for (var i = 0; i < numPatches; i++)
            {
                solid.Solid.Meshes.Add(ReadPatch(br));
            }

            return solid;
        }

        private Face ReadFace(BinaryReader br)
        {
            var face = new Face();

            var renderFlags = br.ReadInt32();

            var numVertices = br.ReadInt32();
            ReadSurfaceProperties(face, br);

            var norm = br.ReadVector3();
            var distance = br.ReadSingle();
            face.Plane = new Plane(norm, distance);
            
            br.ReadInt32(); // something 2

            for (var i = 0; i < numVertices; i++)
            {
                face.Vertices.Add(br.ReadVector3());
                br.ReadVector3(); // texture U/V coordinate
            }

            face.Vertices.Reverse(); // They're backwards...

            return face;
        }

        private Mesh ReadPatch(BinaryReader br)
        {
            var mesh = new Mesh
            {
                Width = br.ReadInt32(),
                Height = br.ReadInt32(),
            };

            ReadSurfaceProperties(mesh, br);

            br.ReadInt32(); // something

            for (var i = 0; i < 32; i++)
            {
                for (var j = 0; j < 32; j++)
                {
                    var point = new MeshPoint
                    {
                        X = i,
                        Y = j,
                        Position = br.ReadVector3(),
                        Normal = br.ReadVector3(),
                        Texture = br.ReadVector3()
                    };

                    if (i < mesh.Width && j < mesh.Height)
                    {
                        mesh.Points.Add(point);
                    }
                }
            }

            return mesh;
        }

        private void ReadSurfaceProperties(Surface surface, BinaryReader br)
        {
            surface.UAxis = br.ReadVector3();
            surface.XShift = br.ReadSingle();
            surface.VAxis = br.ReadVector3();
            surface.YShift = br.ReadSingle();
            surface.XScale = br.ReadSingle();
            surface.YScale = br.ReadSingle();
            surface.Rotation = br.ReadSingle();

            br.ReadInt32(); // something 1
            br.ReadInt32(); // something 2
            br.ReadInt32(); // something 3
            br.ReadInt32(); // something 4

            surface.SurfaceFlags = br.ReadInt32(); // or content flags?
            surface.TextureName = br.ReadFixedLengthString(Encoding.ASCII, 64);
        }

        #endregion

        public void Write(Stream stream, MapFile map, string styleHint)
        {
            using (var bw = new BinaryWriter(stream, Encoding.ASCII, true))
            {
                bw.WriteFixedLengthString(Encoding.ASCII, 4, "JHMF"); // Header

                bw.Write(121); // Version
                
                bw.Write(0); // Num export strings (we don't have this information)
                // No export strings here

                var groups = CreateGroups(map);

                WriteGroups(groups.Values, bw);
                WriteVisgroups(map, bw);
                bw.WriteVector3(map.CordonBounds.min);
                bw.WriteVector3(map.CordonBounds.max);
                WriteCameras(map, bw);
                WritePaths(map, bw);
                WriteEntities(map, groups, bw);
            }
            throw new NotImplementedException();
        }

        private Dictionary<Group, JmfGroup> CreateGroups(MapFile map)
        {
            // In the JMF format, entities cannot have groups within them, only solids - so we only need to traverse nested groups
            var id = 1;
            var ret = new Dictionary<Group, JmfGroup>();
            foreach (var group in map.Worldspawn.Children.OfType<Group>())
            {
                CreateGroupsRecursive(0, group);
            }
            return ret;

            void CreateGroupsRecursive(int parentId, Group group)
            {
                var g = new JmfGroup
                {
                    ID = id,
                    ParentID = parentId,
                    Color = group.Color,
                    Flags = 0,
                    NumObjects = group.FindAll().Count - 1, // includes self so subtract 1
                };
                ret[group] = g;
                id++;
                foreach (var ch in group.Children.OfType<Group>())
                {
                    CreateGroupsRecursive(g.ID, ch);
                }
            }
        }

        private void WriteGroups(IEnumerable<JmfGroup> groups, BinaryWriter bw)
        {
            var list = groups.ToList();
            bw.Write(list.Count);
            foreach (var g in list)
            {
                bw.Write(g.ID);
                bw.Write(g.ParentID);
                bw.Write(g.Flags);
                bw.Write(g.NumObjects);
                bw.WriteRGBAColour(g.Color);
                Console.WriteLine($"Group {g.ID} / {g.ParentID} / {g.NumObjects}");
            }
        }

        private void WriteVisgroups(MapFile map, BinaryWriter bw)
        {
            bw.Write(map.Visgroups.Count);
            foreach (var vg in map.Visgroups)
            {
                WriteString(vg.Name, bw);
                bw.Write(vg.ID);
                bw.WriteRGBAColour(vg.Color);
                bw.Write(vg.Visible);
            }
        }

        private void WriteCameras(MapFile map, BinaryWriter bw)
        {
            bw.Write(map.Cameras.Count);
            foreach (var cam in map.Cameras)
            {
                bw.WriteVector3(cam.EyePosition);
                bw.WriteVector3(cam.LookPosition);
                var flags = JmfFlags.None;
                if (cam.IsActive) flags |= JmfFlags.Selected;
                bw.Write((int) flags);
                bw.WriteRGBAColour(cam.Color);
            }
        }

        private void WritePaths(MapFile map, BinaryWriter bw)
        {
            bw.Write(map.Paths.Count);
            foreach (var path in map.Paths)
            {
                WritePath(path, bw);
            }
        }

        private void WritePath(Path path, BinaryWriter bw)
        {
            throw new NotImplementedException();
        }

        private void WriteEntities(MapFile map, Dictionary<Group, JmfGroup> groups, BinaryWriter bw)
        {
            throw new NotImplementedException();
        }

        private void WriteSolid(Solid solid, Dictionary<Group, JmfGroup> groups, BinaryWriter bw)
        {

        }

        private void WriteFace(Face face, BinaryWriter bw)
        {

        }


        private static string ReadString(BinaryReader br)
        {
            var len = br.ReadInt32();
            if (len < 0) return null;
            var chars = br.ReadChars(len);
            return new string(chars).Trim('\0');
        }

        private static void WriteString(string s, BinaryWriter bw)
        {
            if (s == null)
            {
                bw.Write(0);
            }
            else
            {
                bw.Write(s.Length);
                bw.Write(s.ToCharArray());
            }
        }
        
        private class JmfGroup
        {
            public int ID { get; set; }
            public int ParentID { get; set; }
            public int Flags { get; set; }
            public int NumObjects { get; set; }
            public Color Color { get; set; }
        }
        
        private class JmfEntity
        {
            public int Flags { get; set; }
            public int GroupID { get; set; }
            public Entity Entity { get; set; }
            public List<JmfSolid> Solids { get; set; }

            public JmfEntity()
            {
                Solids = new List<JmfSolid>();
            }
        }
        
        private class JmfSolid
        {
            public int Flags { get; set; }
            public int GroupID { get; set; }
            public Solid Solid { get; set; }
        }


        private enum JmfRendering : int
        {
            Glow = 0x00000025, // HL render modes 3 (glow) and 5 (additive)
            Normal = 0x00000100, // HL render mode 0 (normal)
            Translucent = 0x00040065, // HL render modes 1 (color) and 2 (texture)
            TransparentColorKey = 0x00400165, // HL render mode 4 (solid)
        }

        [Flags]
        private enum JmfFlags
        {
            None = 0x00,
            PointBased = 0x01,   // Or maybe 'HasOrigin'?
            Selected = 0x02,
            Hidden = 0x08,
            RenderMode = 0x10,   // Any other render mode than 'normal'
        }
    }
}
