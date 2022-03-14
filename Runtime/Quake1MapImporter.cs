using UnityEngine;
using System.Collections.Generic;

namespace Scopa {

    public class Quake1MapImporter
    {
        // If true the Textures axis from the Valve map will be used to attempt to align the textures. 
        // this is kindof buggy atm and doesn't work well for angled surfaces.
        public bool adjustTexturesForValve = false;

        /// <summary>
        /// Imports the specified Quake 1 Map Format file.
        /// </summary>
        /// <param name="path">The file path.</param>
        /// <returns>A <see cref="MapData"/> containing the imported world data.</returns>
        public MapData Import(string path) {
            // create a new world.
            MapData world = new MapData();

            world.mapName = Path.GetFileNameWithoutExtension(path);

            // open the file for reading. we use streams for additional performance.
            // it's faster than File.ReadAllLines() as that requires two iterations.
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read))
            using (StreamReader reader = new StreamReader(stream))
            {
                // read all the lines from the file.
                int depth = 0;
                string line;
                bool justEnteredClosure = false;
                bool valveFormat = false;
                string key;
                object value;
                MapBrush brush = null;
                MapEntity entity = null;
                while (!reader.EndOfStream)
                {
                    line = reader.ReadLine().Trim();

                    //UnityEngine.Debug.Log("Line = " + line);


                    if (line.Length == 0) continue;

                    // skip comments.
                    if (line[0] == '/') continue;

                    // parse closures and keep track of them.
                    if (line[0] == '{') { 
                        depth++; justEnteredClosure = true;
                        //UnityEngine.Debug.Log($"Entered Closure Depth = {depth}");
                        continue; }
                    if (line[0] == '}') { depth--;
                        //UnityEngine.Debug.Log($"Exited Closure Depth = {depth}");
                        continue; }

                    // parse entity.
                    if (depth == 1)
                    {
                        // create a new entity and add it to the world.
                        if (justEnteredClosure)
                        {
                            entity = new MapEntity();
                            world.Entities.Add(entity);
                        }

                        // parse entity properties.
                        if (TryParsekeyValue(line, out key, out value))
                        {
                            switch (key)
                            {
                                case "mapversion":
                                    var version = (int)value;
                                    if(version == 220)
                                    {
                                        valveFormat = true;
                                        //world.valveFormat = adjustTexturesForValve;
                                        world.valveFormat = true;
                                    }
                                    //UnityEngine.Debug.Log($"mapversion = {version}");
                                    break;
                                case "classname": 
                                    entity.ClassName = (string)value;
                                    //UnityEngine.Debug.Log($"Classname = {entity.ClassName}");
                                    break;
                                case "_tb_type":
                                    entity.tbType = (string)value;
                                    break;
                                case "_tb_name":
                                    entity.tbName = (string)value;
                                    break;
                                case "_tb_id":
                                    entity.tbId = (int)value;
                                    break;
                                case "_tb_layer":
                                    entity.tbLayer = (int)value;
                                    break;
                                case "_tb_layer_sort_index":
                                    entity.tbLayerSortIndex = (int)value;
                                    break;
                                case "_tb_group":
                                    entity.tbGroup = (int)value;
                                    break;
                            }
                        }
                    }

                    // parse entity brush.
                    if (depth == 2)
                    {
                        // create a new brush and add it to the entity.
                        if (justEnteredClosure)
                        {
                            brush = new MapBrush();
                            entity.Brushes.Add(brush);
                        }

                        // parse brush sides.
                        MapBrushSide mapBrushSide;
                        if (TryParseBrushSide(line, out mapBrushSide, valveFormat))
                        {
                            brush.Sides.Add(mapBrushSide);
                        }
                    }

                    justEnteredClosure = false;
                }
            }

            return world;
        }

        /// <summary>
        /// Tries to parse a key value line.
        /// </summary>
        /// <param name="line">The line (e.g. '"editorversion" "400"').</param>
        /// <param name="key">The key that was found.</param>
        /// <param name="value">The value that was found.</param>
        /// <returns>True if successful else false.</returns>
        private bool TryParsekeyValue(string line, out string key, out object value)
        {
            key = "";
            value = null;

            if (!line.Contains('"')) return false;
            int idx = line.IndexOf('"', 1);

            key = line.Substring(1, idx - 1);
            string rawvalue = line.Substring(idx + 3, line.Length - idx - 4);
            if (rawvalue.Length == 0) return false;

            int vi;
            float vf;
            // detect floating point value.
            if (rawvalue.Contains('.') && float.TryParse(rawvalue, out vf))
            {
                value = vf;
                return true;
            }
            // detect integer value.
            else if (Int32.TryParse(rawvalue, out vi))
            {
                value = vi;
                return true;
            }
            // probably a string value.
            else
            {
                value = rawvalue;
                return true;
            }
        }

        /// <summary>
        /// Tries the parse a brush side line.
        /// </summary>
        /// <param name="line">The line to be parsed.</param>
        /// <param name="mapBrushSide">The map brush side or null.</param>
        /// <returns>True if successful else false.</returns>
        private bool TryParseBrushSide(string line, out MapBrushSide mapBrushSide, bool valveFormat)
        {
            if (valveFormat) return TryParseBrushSideValve(line, out mapBrushSide);

            mapBrushSide = new MapBrushSide();

            // detect brush side definition.
            if (line[0] == '(')
            {
                string[] values = line.Replace("(", "").Replace(")", "").Replace("  ", " ").Replace("  ", " ").Trim().Split(' ');
                if (values.Length != 15) return false;

                try
                {
                    MapVector3 p1 = new MapVector3(float.Parse(values[0], CultureInfo.InvariantCulture), float.Parse(values[1], CultureInfo.InvariantCulture), float.Parse(values[2], CultureInfo.InvariantCulture));
                    MapVector3 p2 = new MapVector3(float.Parse(values[3], CultureInfo.InvariantCulture), float.Parse(values[4], CultureInfo.InvariantCulture), float.Parse(values[5], CultureInfo.InvariantCulture));
                    MapVector3 p3 = new MapVector3(float.Parse(values[6], CultureInfo.InvariantCulture), float.Parse(values[7], CultureInfo.InvariantCulture), float.Parse(values[8], CultureInfo.InvariantCulture));

                    var tex1Vec = new UnityEngine.Vector3(p1.X, p1.Y, p1.Z).normalized;
                    var tex2Vec = new UnityEngine.Vector3(p3.X, p3.Y, p3.Z).normalized;

                    mapBrushSide.t1 = new MapVector3(tex1Vec.x, tex1Vec.y, tex1Vec.z);
                    mapBrushSide.t2 = new MapVector3(tex2Vec.x, tex2Vec.y, tex2Vec.z);

                    mapBrushSide.plane = new MapPlane(p1, p2, p3);
                    mapBrushSide.materialName = values[9];
                    mapBrushSide.offset = new MapVector2(float.Parse(values[10], CultureInfo.InvariantCulture), float.Parse(values[11], CultureInfo.InvariantCulture));
                    mapBrushSide.rotation = float.Parse(values[12], CultureInfo.InvariantCulture);
                    mapBrushSide.scale = new MapVector2(float.Parse(values[13], CultureInfo.InvariantCulture), float.Parse(values[14], CultureInfo.InvariantCulture));
                }
                catch (Exception)
                {
                    throw new Exception("Encountered invalid brush side. The format of the map file must be slightly different, please open an issue on github if you think you did everything right.");
                }

                return true;
            }

            return false;
        }

        private bool TryParseBrushSideValve(string line, out MapBrushSide mapBrushSide)
        {
            mapBrushSide = new MapBrushSide();

            // detect brush side definition.
            if (line[0] == '(')
            {
                string[] values = line.Replace("(", "").Replace(")", "").Replace("[", "").Replace("]", "").Replace("  ", " ").Replace("  ", " ").Trim().Split(' ');

                //UnityEngine.Debug.Log($"Values Length = {values.Length}");

                if (values.Length != 21) return false;

                try
                {
                    MapVector3 p1 = new MapVector3(float.Parse(values[0], CultureInfo.InvariantCulture), float.Parse(values[1], CultureInfo.InvariantCulture), float.Parse(values[2], CultureInfo.InvariantCulture));
                    MapVector3 p2 = new MapVector3(float.Parse(values[3], CultureInfo.InvariantCulture), float.Parse(values[4], CultureInfo.InvariantCulture), float.Parse(values[5], CultureInfo.InvariantCulture));
                    MapVector3 p3 = new MapVector3(float.Parse(values[6], CultureInfo.InvariantCulture), float.Parse(values[7], CultureInfo.InvariantCulture), float.Parse(values[8], CultureInfo.InvariantCulture));
                    mapBrushSide.plane = new MapPlane(p1, p2, p3);
                    mapBrushSide.materialName = values[9];

                    mapBrushSide.t1 = new MapVector3(float.Parse(values[10], CultureInfo.InvariantCulture), float.Parse(values[11], CultureInfo.InvariantCulture), float.Parse(values[12], CultureInfo.InvariantCulture));
                    mapBrushSide.t2 = new MapVector3(float.Parse(values[14], CultureInfo.InvariantCulture), float.Parse(values[15], CultureInfo.InvariantCulture), float.Parse(values[16], CultureInfo.InvariantCulture));

                    mapBrushSide.offset = new MapVector2(float.Parse(values[13], CultureInfo.InvariantCulture), float.Parse(values[17], CultureInfo.InvariantCulture));
                    mapBrushSide.rotation = float.Parse(values[18], CultureInfo.InvariantCulture);
                    mapBrushSide.scale = new MapVector2(float.Parse(values[19], CultureInfo.InvariantCulture), float.Parse(values[20], CultureInfo.InvariantCulture));
                }
                catch (Exception)
                {
                    throw new Exception("Encountered invalid brush side. The format of the map file must be slightly different, please open an issue on github if you think you did everything right.");
                }

                return true;
            }

            return false;
        }
    }
}
