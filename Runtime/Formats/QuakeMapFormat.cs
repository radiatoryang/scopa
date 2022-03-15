using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Assertions;
using System.Text;
using Scopa;
using Scopa.Formats.Map.Objects;

namespace Scopa.Formats.Map.Formats
{
    /*  Quake format
     *  {
     *      "classname" "worldspawn"
     *      "key" "value"
     *      "spawnflags" "0"
     *      {
     *          // idTech2:
     *          ( x y z ) ( x y z ) ( x y z ) texturename xshift yshift rotation xscale yscale
     *          // idTech3:
     *          ( x y z ) ( x y z ) ( x y z ) shadername xshift yshift rotation xscale yscale contentflags surfaceflags value
     *          // Worldcraft:
     *          ( x y z ) ( x y z ) ( x y z ) texturename [ ux uy uz xshift ] [ vx vy vz yshift ] rotation xscale yscale
     *      }
     *  }
     *  {
     *      "spawnflags" "0"
     *      "classname" "entityname"
     *      "key" "value"
     *  }
     *  {
     *      "spawnflags" "0"
     *      "classname" "entityname"
     *      "key" "value"
     *      {
     *          ( x y z ) ( x y z ) ( x y z ) texturename xoff yoff rot xscale yscale
     *      }
     *  }
     *  {
     *      patchDef2 // idTech3 ONLY
     *      {
     *          shadername
     *          ( width height 0 0 0 )
     *          (
     *              ( ( x y z u v ) ... ( x y z u v ) )
     *          )
     *          }
     *      }
     *  }
     *  {
     *      brushDef // idTech3 ONLY
     *      {
     *          ( x y z ) ( x y z ) ( x y z ) ( ( ux uy uz ) ( vx vy vz ) ) shadername contentflags surfaceflags value
     *      }
     *  }
     *  {
     *      brushDef3 // idTech4 ONLY
     *      {
     *          ?
     *      }
     *  }
     *  {
     *      patchDef3 // idTech4 ONLY
     *      {
     *          ?
     *      }
     *  }
     */
    public class QuakeMapFormat : IMapFormat
    {
        public string Name => "Quake Map";
        public string Description => "The .map file format used for most Quake editors.";
        public string ApplicationName => "Radiant";
        public string Extension => "map";
        public string[] AdditionalExtensions => new[] { "max" };
        public string[] SupportedStyleHints => new[] { "idTech2", "idTech3", "idTech4", "Worldcraft" };

        public MapFile Read(Stream stream)
        {
            var map = new MapFile();
            using (var rdr = new StreamReader(stream, Encoding.ASCII, true, 1024, true))
            {
                ReadEntities(rdr, map);
            }
            return map;
        }

        #region Read

        private static string CleanLine(string line)
        {
            if (line == null) return null;
            var ret = line;
            if (ret.Contains("//")) ret = ret.Substring(0, ret.IndexOf("//", StringComparison.Ordinal)); // Comments
            return ret.Trim();
        }

        private static void ReadEntities(StreamReader rdr, MapFile map)
        {
            string line;
            while ((line = CleanLine(rdr.ReadLine())) != null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                if (line == "{") ReadEntity(rdr, map);
            }
        }

        private static void ReadEntity(StreamReader rdr, MapFile map)
        {
            var e = new Entity();

            string line;
            while ((line = CleanLine(rdr.ReadLine())) != null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                if (line[0] == '"')
                {
                    ReadProperty(e, line);
                }
                else if (line[0] == '{')
                {
                    var s = ReadSolid(rdr);
                    if (s != null) e.Children.Add(s);
                }
                else if (line[0] == '}')
                {
                    break;
                }
            }

            if (e.ClassName == "worldspawn")
            {
                map.Worldspawn.SpawnFlags = e.SpawnFlags;
                foreach (var p in e.Properties) map.Worldspawn.Properties[p.Key] = p.Value;
                map.Worldspawn.Children.AddRange(e.Children);
            }
            else
            {
                map.Worldspawn.Children.Add(e);
            }
        }

        private static void ReadProperty(Entity ent, string line)
        {
            // Quake id1 map sources use tabs between keys and values
            var split = line.Split(' ', '\t');
            var key = split[0].Trim('"');

            var val = string.Join(" ", split.Skip(1)).Trim('"');

            if (key == "classname")
            {
                ent.ClassName = val;
            }
            else if (key == "spawnflags")
            {
                ent.SpawnFlags = int.Parse(val);
            }
            else
            {
                ent.Properties[key] = val;
            }
        }

        private static Solid ReadSolid(StreamReader rdr)
        {
            var s = new Solid();

            string line;
            while ((line = CleanLine(rdr.ReadLine())) != null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                switch (line)
                {
                    case "}":
                        s.ComputeVertices();
                        return s;
                    case "patchDef2":
                    case "brushDef":
                        Util.Assert(false, "idTech3 format maps are currently not supported.");
                        break;
                    case "patchDef3":
                    case "brushDef3":
                        Util.Assert(false, "idTech4 format maps are currently not supported.");
                        break;
                    default:
                        s.Faces.Add(ReadFace(line));
                        break;
                }
            }
            return null;
        }

        private static Face ReadFace(string line)
        {
            const NumberStyles ns = NumberStyles.Float;

            var parts = line.Split(' ').ToList();
            // Debug.Log(line);

            Util.Assert(parts[0] == "(");
            Util.Assert(parts[4] == ")");
            Util.Assert(parts[5] == "(");
            Util.Assert(parts[9] == ")");
            Util.Assert(parts[10] == "(");
            Util.Assert(parts[14] == ")");

            var a = NumericsExtensions.Parse(parts[1], parts[2], parts[3], ns, CultureInfo.InvariantCulture);
            var b = NumericsExtensions.Parse(parts[6], parts[7], parts[8], ns, CultureInfo.InvariantCulture);
            var c = NumericsExtensions.Parse(parts[11], parts[12], parts[13], ns, CultureInfo.InvariantCulture);

            // var ab = b - a;
            // var ac = c - a;

            // var normal = Vector3.Cross(ac, ab).normalized;
            // var d = normal.Dot(a);

            var face = new Face()
            {
                Plane = new Plane(a, b, c),
                TextureName = parts[15]
            };
            // Debug.Log($"normal: {face.Plane.normal}, distance: {face.Plane.distance}");

            // idTech2, idTech3
            if (parts.Count == 21 || parts.Count == 24)
            {
                var direction = face.Plane.GetClosestAxisToNormal();
                face.UAxis = direction == Vector3.right ? Vector3.up : Vector3.right;
                face.VAxis = direction == Vector3.forward ? -Vector3.up : -Vector3.forward;

                var xshift = float.Parse(parts[16], ns, CultureInfo.InvariantCulture);
                var yshift = float.Parse(parts[17], ns, CultureInfo.InvariantCulture);
                var rotate = float.Parse(parts[18], ns, CultureInfo.InvariantCulture);
                var xscale = float.Parse(parts[19], ns, CultureInfo.InvariantCulture);
                var yscale = float.Parse(parts[20], ns, CultureInfo.InvariantCulture);

                face.Rotation = rotate;
                face.XScale = xscale;
                face.YScale = yscale;
                face.XShift = xshift;
                face.YShift = yshift;

                // idTech3
                if (parts.Count == 24)
                {
                    face.ContentFlags = int.Parse(parts[18], CultureInfo.InvariantCulture);
                    face.SurfaceFlags = int.Parse(parts[19], CultureInfo.InvariantCulture);
                    face.Value = float.Parse(parts[20], ns, CultureInfo.InvariantCulture);
                }
            }
            // Worldcraft
            else if (parts.Count == 31)
            {
                Util.Assert(parts[16] == "[");
                Util.Assert(parts[21] == "]");
                Util.Assert(parts[22] == "[");
                Util.Assert(parts[27] == "]");

                face.UAxis = NumericsExtensions.Parse(parts[17], parts[18], parts[19], ns, CultureInfo.InvariantCulture);
                face.XShift = float.Parse(parts[20], ns, CultureInfo.InvariantCulture);
                face.VAxis = NumericsExtensions.Parse(parts[23], parts[24], parts[25], ns, CultureInfo.InvariantCulture);
                face.YShift = float.Parse(parts[26], ns, CultureInfo.InvariantCulture);
                face.Rotation = float.Parse(parts[28], ns, CultureInfo.InvariantCulture);
                face.XScale = float.Parse(parts[29], ns, CultureInfo.InvariantCulture);
                face.YScale = float.Parse(parts[30], ns, CultureInfo.InvariantCulture);
            }
            else
            {
                Util.Assert(false, $"Unknown number of tokens ({parts.Count}) in face definition.");
            }

            return face;
        }

        #endregion

        public void Write(Stream stream, MapFile map, string styleHint)
        {
            using (var sw = new StreamWriter(stream, Encoding.ASCII, 1024, true))
            {
                WriteWorld(sw, map.Worldspawn, styleHint);
            }
        }

        #region Writing


        private static string FormatVector3(Vector3 c)
        {
            return $"{c.x.ToString("0.000", CultureInfo.InvariantCulture)} {c.y.ToString("0.000", CultureInfo.InvariantCulture)} {c.z.ToString("0.000", CultureInfo.InvariantCulture)}";
        }

        private static void CollectNonEntitySolids(List<Solid> solids, MapObject parent)
        {
            foreach (var obj in parent.Children)
            {
                switch (obj)
                {
                    case Solid s:
                        solids.Add(s);
                        break;
                    case Group _:
                        CollectNonEntitySolids(solids, obj);
                        break;
                }
            }
        }

        private static void CollectEntities(List<Entity> entities, MapObject parent)
        {
            foreach (var obj in parent.Children)
            {
                switch (obj)
                {
                    case Entity e:
                        entities.Add(e);
                        break;
                    case Group _:
                        CollectEntities(entities, obj);
                        break;
                }
            }
        }

        private static void WriteFace(StreamWriter sw, Face face, string styleHint)
        {
            // ( -128 64 64 ) ( -64 64 64 ) ( -64 0 64 ) AAATRIGGER [ 1 0 0 0 ] [ 0 -1 0 0 ] 0 1 1
            var strings = face.Vertices.Take(3).Select(x => "( " + FormatVector3(x) + " )").ToList();
            strings.Add(String.IsNullOrWhiteSpace(face.TextureName) ? "NULL" : face.TextureName);
            switch (styleHint)
            {
                case "idTech2":
                    strings.Add("[");
                    strings.Add(face.XShift.ToString("0.000", CultureInfo.InvariantCulture));
                    strings.Add(face.YShift.ToString("0.000", CultureInfo.InvariantCulture));
                    strings.Add(face.Rotation.ToString("0.000", CultureInfo.InvariantCulture));
                    strings.Add(face.XScale.ToString("0.000", CultureInfo.InvariantCulture));
                    strings.Add(face.YScale.ToString("0.000", CultureInfo.InvariantCulture));
                    break;
                case "idTech3":
                    Util.Assert(false, "idTech3 format maps are currently not supported.");
                    break;
                case "idTech4":
                    Util.Assert(false, "idTech4 format maps are currently not supported.");
                    break;
                case "Worldcraft":
                default:
                    strings.Add("[");
                    strings.Add(FormatVector3(face.UAxis));
                    strings.Add(face.XShift.ToString("0.000", CultureInfo.InvariantCulture));
                    strings.Add("]");
                    strings.Add("[");
                    strings.Add(FormatVector3(face.VAxis));
                    strings.Add(face.YShift.ToString("0.000", CultureInfo.InvariantCulture));
                    strings.Add("]");
                    strings.Add(face.Rotation.ToString("0.000", CultureInfo.InvariantCulture));
                    strings.Add(face.XScale.ToString("0.000", CultureInfo.InvariantCulture));
                    strings.Add(face.YScale.ToString("0.000", CultureInfo.InvariantCulture));
                    break;
            }

            sw.WriteLine(String.Join(" ", strings));
        }

        private static void WriteSolid(StreamWriter sw, Solid solid, string styleHint)
        {
            sw.WriteLine("{");
            foreach (var face in solid.Faces)
            {
                WriteFace(sw, face, styleHint);
            }
            sw.WriteLine("}");
        }

        private static void WriteProperty(StreamWriter sw, string key, string value)
        {
            sw.WriteLine('"' + key + "\" \"" + value + '"');
        }

        private static void WriteEntity(StreamWriter sw, Entity ent, string styleHint)
        {
            var solids = new List<Solid>();
            CollectNonEntitySolids(solids, ent);
            WriteEntityWithSolids(sw, ent, solids, styleHint);
        }

        private static void WriteEntityWithSolids(StreamWriter sw, Entity e, IEnumerable<Solid> solids, string styleHint)
        {
            sw.WriteLine("{");

            WriteProperty(sw, "classname", e.ClassName);

            if (e.SpawnFlags != 0)
            {
                WriteProperty(sw, "spawnflags", e.SpawnFlags.ToString(CultureInfo.InvariantCulture));
            }

            foreach (var prop in e.Properties)
            {
                WriteProperty(sw, prop.Key, prop.Value);
            }

            foreach (var s in solids)
            {
                WriteSolid(sw, s, styleHint);
            }

            sw.WriteLine("}");
        }

        private void WriteWorld(StreamWriter sw, Worldspawn world, string styleHint)
        {
            var solids = new List<Solid>();
            var entities = new List<Entity>();

            CollectNonEntitySolids(solids, world);
            CollectEntities(entities, world);

            WriteEntityWithSolids(sw, world, solids, styleHint);

            foreach (var entity in entities)
            {
                WriteEntity(sw, entity, styleHint);
            }
        }

        #endregion
    }
}
