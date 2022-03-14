using UnityEngine;
using System.Collections.Generic;

namespace Scopa {

    /// <summary> Container for converting map data to Unity equivalents (meshes, asset references, game objects) </summary>
    public class MapWorld : MonoBehaviour {
        public MapData mapData;
        public Dictionary<Transform, MapEntity> transformToEntity = new Dictionary<Transform, MapEntity>();

        /// <summary> When importing, how many .MAP units to correspond to 1 Unity unit (usually the equivalent of 1.0 meters) </summary>
        public int worldScaleMeter = 32;

        /// <summary> Internal scaling factor to apply to meshes, based on scaleMeter </summary>
        public float worldScalar { get { return 1f / worldScaleMeter; } }

    }

    /// <summary> Parsed map data, not converted into Unity equivalent yet. </summary>
    public class MapData {
        /// <summary> All world brushes (or null if no world brushes). Does not contain brush entities!</summary>
        public List<MapBrush> Brushes {
            get {
                return Entities.Where(e => e.ClassName == "worldspawn").Select(e => e.Brushes).FirstOrDefault();
            }
        }

        /// <summary> All the entities in the world, including the world itself ("worldspawn")</summary>
        public List<MapEntity> Entities = new List<MapEntity>();

        /// <summary> Does this map use Valve version 220 for texture alignment? </summary>
        public bool valveFormat = false;

        public string mapName;

    }

    /// <summary> base class for .MAP entity </summary>
    public class MapEntity {
        /// <summary> The class name of the entity, like "func_door" or "trigger_multiple" </summary>
        public string ClassName;

        public string tbType, tbName;
        public int tbId = -1;
        public int tbLayerSortIndex = -1;
        public int tbGroup = -1;
        public int tbLayer = -1;

        /// <summary> If it's a brush entity, this is a List of its associated MapBrushes. </summary>
        public List<MapBrush> Brushes = new List<MapBrush>();

        public override string ToString() {
            return "Entity " + ClassName + " (" + Brushes.Count + " Brushes)";
        }
    }

    /// <summary> base class for .MAP brush </summary>
    public class MapBrush {
        public List<MapBrushSide> Sides = new List<MapBrushSide>();

        public override string ToString() {
            return "Brush " + " (" + Sides.Count + " Sides)";
        }
    }

    /// <summary> base class for .MAP brush side </summary>
    public class MapBrushSide {
        public Plane plane;
        public string materialName;
        public Vector2 scale, offset;
        public float rotation;
        public Vector3 t1, t2;

        public override string ToString() {
            return "Brush Side " + " '" + materialName + "' " + " " + plane;
        }

        public Matrix4x4 ToMatrix() {
            var x = t1 * scale.x;
            var y = t2 * scale.y;
            var z = Vector3.Cross(t1, t2);

            return new Matrix4x4(
                new Vector4(x.x, x.y, x.z, offset.x),
                new Vector4(y.x, y.y, y.z, offset.y),
                new Vector4(z.x, z.y, z.z, 0.0f),
                new Vector4(0.0f, 0.0f, 0.0f, 1.0f)
            );
        }

    }

    /// <summary> base class for .MAP brush axis </summary>
    public class MapAxis {
        public Vector3 Vector;
        public float Translation;
        public float Scale;

        public MapAxis(Vector3 vector, float translation, float scale) {
            Vector = vector;
            Translation = translation;
            Scale = scale;
        }

        public override string ToString() {
            return "Axis " + Vector + ", T=" + Translation + ", S=" + Scale;
        }
    }
}