using System;
using UnityEngine;
using UnityEngine.Serialization;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Rendering;

namespace Scopa {
    /// <summary> ScriptableObject to use for configuring how Scopa imports .MAPs, even for runtime imports too. </summary>
    [CreateAssetMenu(fileName = "New ScopaMapConfig", menuName = "Scopa/MAP Config Asset", order = 1)]
    public class ScopaMapConfigAsset : ScriptableObject {
        public ScopaMapConfig config = new ScopaMapConfig();
    }

    [System.Serializable]
    public class ScopaMapConfig {
        [Header("MESHES")]

        [Tooltip("(default: 0.03125, 1 m = 32 units) The global scaling factor for all brush geometry and entity origins.")]
        public float scalingFactor = 0.03125f;

        // TODO: revisit vertex snapping later? all methods tested seemed to add MORE cracks and seams?
        // also, Burst'd maps + high precision seems to minimize a lot of seams already?

        // [Tooltip("(default: 1) vertex snap distance threshold in unscaled map units. Pretty important for minimizing seams and cracks on complex non-rectilinear brushes. In the map editor, avoid building smaller than this threshold. Set to 0 to disable for slightly faster import times, but you may get more seams and hairline cracks.")]
        // public float snappingThreshold = 1f;

        [Tooltip("(default: 80 degrees) smooth shading on edges, which adds extra import time; set to -1 to disable default global smoothing, and/or override with _phong / _phong_angle entity keyvalues")]
        public float defaultSmoothingAngle = 80f;

        [Tooltip("(default: true) Try to detect whether a face is completely covered by another face within the same entity, and discard it. It's far from perfect; it can't detect if a face is covered by 2+ faces. But it helps. Note the extra calculations increase map import times.")]
        public bool removeHiddenFaces = true;

        [Tooltip("(default: true) Generate tangent data needed for normal mapping. If you're not using normal maps, disable for small memory savings.")]
        public bool addTangents = true;

        [Tooltip("(EDITOR-ONLY) (default: false) Generate lightmap UVs using Unity's built-in lightmap unwrapper. Be warned - for large maps, this can be very slow / broken. If you're not using lightmaps, maybe disable for small memory savings.")]
        public bool addLightmapUV2 = false;

        [Tooltip("(default: None) Optimize the mesh using Unity's built-in mesh optimization methods. Optimizes the mesh for rendering performance, but may increase import times... or even crash Unity.")]
        public ModelImporterMeshOptimization optimizeMesh = ModelImporterMeshOptimization.None;
        [Tooltip("(EDITOR-ONLY) (default: Off) Use Unity's built-in mesh compressor. Reduces file size but may cause glitches and seams.")]
        public ModelImporterMeshCompression meshCompression = ModelImporterMeshCompression.Off;


        [Space(), Header("TEXTURES & MATERIALS")]

        [Tooltip("(EDITOR-ONLY) (default: true) try to automatically match each texture name to a similarly named Material already in the project. Increases import times based on how many Materials it must find.")]
        public bool findMaterials = true;

        [Tooltip("(optional) manually set a specific Material for each texture name")]
        public MaterialOverride[] materialOverrides;


        [Space(), Header("GAME OBJECTS & PREFABS")]

        [Tooltip("(default: Concave Mesh Collider) Concave mesh collider is convenient and OK for most uses, but can be buggy at high velocities / expensive for a big complex map. If you need accuracy / stability, use Box and Convex - but be warned Unity makes each brush collider have its own game object, which can add lots of overhead for big complex maps.\nNote: Triggers can't be concave, and will default to Box / Convex.")]
        public ColliderImportMode colliderMode = ColliderImportMode.MergeAllToOneConcaveMeshCollider;

        [Tooltip("(default: Default) Set ALL objects to use this layer. Overridden by Entity Prefab / Mesh Prefab settings below.")]
        [Layer] public int layer = 0;

        [Tooltip("(default: On) the shadow casting mode on all the mesh objects. Overridden by Mesh Prefab setting below.")]
        public UnityEngine.Rendering.ShadowCastingMode castShadows = UnityEngine.Rendering.ShadowCastingMode.TwoSided;

        [Tooltip("(optional) Prefab template to use for the root of EVERY entity. Ignores the Layer setting above.")]
        public GameObject entityPrefab;
        
        [Tooltip("(optional) Prefab template to use for each mesh + material in each entity. meshFilter.sharedMesh and meshRenderer.sharedMaterial will be overridden. Useful for setting layers, renderer settings, etc. Ignores the Layer and Cast Shadows settings above.")]
        public GameObject meshPrefab;

        [Tooltip("(optional) For maximum control, set specific prefabs for each entity type in an FGD asset.")]
        public ScopaFgdConfigAsset fgdAsset;


        static Material builtinDefaultMaterial = null;

        /// <summary> NOT case sensitive </summary>
        public bool IsTextureNameCulled(string textureNameSearch) {
            if ( string.IsNullOrWhiteSpace(textureNameSearch) )
                return true;

            var search = textureNameSearch.ToLowerInvariant();
            var cullTextures = ScopaProjectSettings.GetCached().cullTextures;
            for(int i=0; i<cullTextures.Count; i++) {
                if ( search.Contains(cullTextures[i]) ) {
                    return true;
                }
            }
            return false;
        }

        /// <summary> NOT case sensitive </summary>
        public bool IsEntityMergeToWorld(string entityClassnameSearch) {
            var search = entityClassnameSearch.ToLowerInvariant();
            var mergeToWorld = ScopaProjectSettings.GetCached().mergeToWorld;
            for(int i=0; i<mergeToWorld.Count; i++) {
                if ( search.Contains(mergeToWorld[i]) ) {
                    return true;
                }
            }
            return false;
        }

        /// <summary> NOT case sensitive </summary>
        public bool IsEntityStatic(string entityClassname) {
            var search = entityClassname.ToLowerInvariant();
            var staticEntities = ScopaProjectSettings.GetCached().staticEntities;
            for(int i=0; i<staticEntities.Count; i++) {
                if ( search.Contains(staticEntities[i]) ) {
                    return true;
                }
            }
            return false;
        }

        /// <summary> NOT case sensitive </summary>
        public bool IsEntityNonsolid(string entityClassname) {
            var search = entityClassname.ToLowerInvariant();
            var nonsolidEntities = ScopaProjectSettings.GetCached().nonsolidEntities;
            for(int i=0; i<nonsolidEntities.Count; i++) {
                if ( search.Contains(nonsolidEntities[i]) ) {
                    return true;
                }
            }
            return false;
        }

        /// <summary> NOT case sensitive </summary>
        public bool IsEntityTrigger(string entityClassname) {
            var search = entityClassname.ToLowerInvariant();
            var triggerEntities = ScopaProjectSettings.GetCached().triggerEntities;
            for(int i=0; i<triggerEntities.Count; i++) {
                if ( search.Contains(triggerEntities[i]) ) {
                    return true;
                }
            }
            return false;
        }

        /// <summary> NOT case sensitive </summary>
        public MaterialOverride GetMaterialOverrideFor(string textureNameSearch) {
            if ( materialOverrides == null || materialOverrides.Length == 0) {
                return null;
            }

            textureNameSearch = textureNameSearch.ToLowerInvariant();
            var search = materialOverrides.Where( 
                ov => ov.allowPartialMatch ? textureNameSearch.Contains(ov.GetTextureNameForSearching()) : textureNameSearch == ov.GetTextureNameForSearching() 
            ).FirstOrDefault();
            return search;
        }

        public Material GetDefaultMaterial() {
            if (ScopaProjectSettings.GetCached().defaultMaterial != null)
                return ScopaProjectSettings.GetCached().defaultMaterial;

            #if UNITY_EDITOR

            if (builtinDefaultMaterial == null)
                builtinDefaultMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>( 
                    GraphicsSettings.currentRenderPipeline != null ?
                    "Packages/com.radiatoryang.scopa/Runtime/Textures/BlockoutLightURP.mat"
                    : "Packages/com.radiatoryang.scopa/Runtime/Textures/BlockoutLight.mat" 
                );
            if (builtinDefaultMaterial == null )
                builtinDefaultMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>( 
                    GraphicsSettings.currentRenderPipeline != null ?
                    UnityEditor.AssetDatabase.FindAssets("BlockoutLightURP.mat")[0] 
                    : UnityEditor.AssetDatabase.FindAssets("BlockoutLight.mat")[0] 
                );
            if (builtinDefaultMaterial == null )
                builtinDefaultMaterial = UnityEditor.AssetDatabase.GetBuiltinExtraResource<Material>("Default-Diffuse.mat");
            return builtinDefaultMaterial;

            #else

            // terrible hacky way to get default material at runtime https://answers.unity.com/questions/390513/how-do-i-apply-default-diffuse-material-to-a-meshr.html
            if (builtinDefaultMaterial == null) {
                GameObject primitive = GameObject.CreatePrimitive(PrimitiveType.Plane);
                primitive.SetActive(false);
                builtinDefaultMaterial = primitive.GetComponent<MeshRenderer>().sharedMaterial;
                GameObject.Destroy(primitive);
            }
            return builtinDefaultMaterial;

            #endif

        }

        /// <summary> note: entityClassname must already be ToLowerInvariant() </summary>
        public GameObject GetEntityPrefabFor(string entityClassname) {
            // special early out for default case
            // if ( fgdAsset == null ) {
            //     return entityPrefab;
            // }

            // try looking in the MAP config
            // var search = entityOverrides.Where( cfg => entityClassname.Contains(cfg.entityClassName.ToLowerInvariant()) ).FirstOrDefault();
            // if ( search != null && search.entityPrefab != null) {
            //     return search.entityPrefab;
            // }

            // try looking in the FGD config
            if ( fgdAsset != null ) {
                var fgdSearch = fgdAsset.config.GetEntityPrefabFor(entityClassname);
                if ( fgdSearch != null) {
                    // Debug.Log("found FGD prefab for " + entityClassname);
                    return fgdSearch;
                }
            }

            return entityPrefab;
        }

        /// <summary> note: entityClassname must already be ToLowerInvariant() </summary>
        public GameObject GetMeshPrefabFor(string entityClassname) {
            // if ( entityOverrides == null || entityOverrides.Length == 0) {
            //     return meshPrefab;
            // }

            // var search = entityOverrides.Where( cfg => entityClassname.Contains(cfg.entityClassName.ToLowerInvariant()) ).FirstOrDefault();
            // if ( search != null && search.meshPrefab != null) {
            //     return search.meshPrefab;
            // }

            if( fgdAsset != null) {
                var fgdSearch = fgdAsset.config.GetMeshPrefabFor(entityClassname);
                if ( fgdSearch != null) {
                    // Debug.Log("found FGD prefab for " + entityClassname);
                    return fgdSearch;
                }
            }

            return meshPrefab;
        }


        [System.Serializable]
        public class EntityOverride {
            [Tooltip("for example: func_detail, func_wall, light, etc... worldspawn is for world brushes. Partial matches count, e.g. 'func' will match all func_ entities.")]
            public string entityClassName;

            [Tooltip("the prefabs to use, just for this entity type")]
            public GameObject entityPrefab, meshPrefab;
        }

        [System.Serializable]
        public class MaterialOverride {
            [Tooltip("(optional) If a face's texture name matches this, then use this Material no matter what. If empty, then use Material's name.")]
            [SerializeField, FormerlySerializedAs("textureName")]
            private string textureNameOverride;

            [Tooltip("(default: false) if true, then partial texture name matches count, e.g. an override for 'stone' will match all faces with texture names that contain the word 'stone'")]
            public bool allowPartialMatch = false;

            public Material material;

            [Tooltip("(optional) use this to add additional auto-UV / auto-detail treatments to brushes with this texture")]
            [FormerlySerializedAs("hotspotAtlas")]
            public ScopaMaterialConfig materialConfig;

            public MaterialOverride(string texName, Material mat) {
                this.textureNameOverride = texName;
                this.material = mat;
            }

            public string GetTextureNameForSearching() {
                if (string.IsNullOrWhiteSpace(textureNameOverride))
                    return material.name.ToLowerInvariant();
                else
                    return textureNameOverride.ToLowerInvariant();
            }
        }

        public enum ColliderImportMode {
            None,
            ConvexMeshColliderOnly,
            BoxAndConvex,
            MergeAllToOneConcaveMeshCollider
        }

        public enum ModelImporterMeshCompression
        {
            Off = 0,
            Low = 1,
            Medium = 2,
            High = 3
        }

        [Flags]
        public enum ModelImporterMeshOptimization
        {
            None = 0,
            OptimizeIndexBuffers = 1,
            OptimizeVertexBuffers = 2,
        }

        public ScopaMapConfig ShallowCopy() {
            return (ScopaMapConfig) this.MemberwiseClone();
        }
    }

    
}

