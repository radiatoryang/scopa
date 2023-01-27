using System.Collections.Generic;
using System.Linq;
using Sledge.Formats.Map.Objects;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;
using Mesh = UnityEngine.Mesh;

namespace Scopa
{
    /// <summary> ScriptableObject asset used to configure hotspot UVs, surface detail instancing, and maybe more. </summary>
    [CreateAssetMenu(fileName = "New Scopa Material Config", menuName = "Scopa/Material Config", order = 300)]
    public class ScopaMaterialConfig : ScriptableObject
    {
        [Tooltip("if defined, Scopa will use this mesh prefab when instantiating brush meshes\n (NOTE: if the map importer or entity config has a mesh prefab defined, that will override this")]
        public GameObject meshPrefab;

        [Tooltip("(default: -1) if 0 or higher, this value will override the map config settings' smoothing angle for meshes with this material\n (note: entities can override *this* setting with _phong and _phong_angle)")]
        public float smoothingAngle = -1;

        /// <summary>make a new class that inherits from ScopaMaterialConfig + override OnPrepassBrushFace()
        /// to modify raw brush face data and material selection at .MAP import time; returning a null Material will discard the brush face</summary>
        public virtual Material OnPrepassBrushFace(Solid brush, Face face, ScopaMapConfig mapConfig, Material faceMaterial) { return faceMaterial; }

        /// <summary>make a new class that inherits from ScopaMaterialConfig + override OnBuildBrushFace()
        /// to modify face mesh vertex data at .MAP import time... two important notes: 
        /// (1) if you modify verts and UVs, make sure it's in the correct order, and 
        /// (2) triangle index list is based on the entire mesh, so indices probably don't start at 0, e.g. faceMeshTris[0] probably won't be 0</summary>
        public virtual void OnBuildBrushFace(Solid brush, Face face, ScopaMapConfig mapConfig, List<Vector3> faceMeshVerts, List<Vector2> faceMeshUVs, List<int> faceMeshTris) { }

        /// <summary>make a new class that inherits from ScopaMaterialConfig + override OnBuildMeshObject()
        /// to add custom components / access mesh data at .MAP import time</summary>
        public virtual void OnBuildMeshObject(GameObject meshObject, Mesh mesh) { }
    }
}
