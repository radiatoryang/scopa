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

        /// <summary>enables a custom ScopaMaterialConfig.OnBuildMeshObject()</summary>
        [Tooltip("(default: false) if you code a custom ScopaMaterialConfig, set this to true and override OnBuildMeshObject() to add custom per-material components / modify mesh data at .MAP import time")]
        public bool useOnBuildMeshObject = false;
        
        /// <summary>To use this, make a new class that inherits from ScopaMaterialConfig + override OnBuildMeshObject() to add custom components / modify mesh data at .MAP import time
        /// ... and don't forget to set useOnBuildMeshObject=true in the inspector.</summary>
        public virtual void OnBuildMeshObject(GameObject meshObject, ScopaRendererMeshResult meshResult, ScopaMesh.ScopaMeshJobGroup jobsData) { }
    }
}
