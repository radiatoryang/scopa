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

        /// <summary>enables a custom ScopaMaterialConfig.OnPrepassBrushFace()</summary>
        [Tooltip("(default: false) if you code a custom ScopaMaterialConfig, set this to true and override OnPrepassBrushFace() to modify raw brush face data and material selection at .MAP import time")]
        public bool useOnPrepassBrushFace = false;

        /// <summary>To use this, make a new class that inherits from ScopaMaterialConfig + override OnPrepassBrushFace() to modify raw brush face data and material selection at .MAP import time; returning a null Material will discard the brush face
        /// ... and don't forget to enable useOnPrepassBrushFace=true in the inspector.</summary>
        public virtual Material OnPrepassBrushFace(Solid brush, Face face, ScopaMapConfig mapConfig, Material faceMaterial) { return faceMaterial; }

        /// <summary>enables a custom ScopaMaterialConfig.OnBuildBrushFace()</summary>
        [Tooltip("(default: false) if you code a custom ScopaMaterialConfig, set this to true and override OnBuildBrushFace() to modify unscaled brush face vertices and override UVs at import time")]
        public bool useOnBuildBrushFace = false;

        /// <summary>To use this, make a new class that inherits from ScopaMaterialConfig + override OnBuildBrushFace() to modify face mesh vertex data at .MAP import time. 
        /// ... and don't forget to set useOnBuildBrushFace=true in the inspector.
        /// IMPORTANT NOTES:
        /// (1) don't resize face.Vertices array! the UV override array must be same size as face.Vertices!
        /// (2) return true if you actually override the UVs, which will copy over the face UV data... otherwise return false</summary>
        public virtual bool OnBuildBrushFace(Face face, ScopaMapConfig mapConfig, out Vector2[] faceMeshUVOverride) { faceMeshUVOverride = null; return false; }

        /// <summary>enables a custom ScopaMaterialConfig.OnBuildMeshObject()</summary>
        [Tooltip("(default: false) if you code a custom ScopaMaterialConfig, set this to true and override OnBuildMeshObject() to to add custom per-material components / modify mesh data at .MAP import time")]
        public bool useOnBuildMeshObject = false;
        
        /// <summary>To use this, make a new class that inherits from ScopaMaterialConfig + override OnBuildMeshObject() to add custom components / modify mesh data at .MAP import time
        /// ... and don't forget to set useOnBuildMeshObject=true in the inspector.</summary>
        public virtual void OnBuildMeshObject(GameObject meshObject, Mesh mesh) { }

        /// <summary>enables a custom ScopaMaterialConfig.OnPostBuildMeshObject()</summary>
        [Tooltip("(default: false) if you code a custom ScopaMaterialConfig, set this to true and override OnPostBuildMeshObject() to to add custom per-material components / modify mesh data *AFTER* .MAP complete import (colliders done, raycasts work)")]
        public bool useOnPostBuildMeshObject = false;
        
        /// <summary>To use this, make a new class that inherits from ScopaMaterialConfig + override OnPostBuildMeshObject() to add custom components / modify mesh data *AFTER* .MAP complete import (colliders done, raycasts work)
        /// ... and don't forget to set useOnPostBuildMeshObject=true in the inspector.</summary>
        public virtual void OnPostBuildMeshObject(GameObject meshObject, Mesh mesh, ScopaEntityData entityData) { }
    }
}
