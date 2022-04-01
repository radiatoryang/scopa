using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Scopa {
    /// <summary> ScriptableObject to use for configuring how Scopa imports .MAPs, even for runtime imports too. </summary>
    [CreateAssetMenu(fileName = "New ScopaMapConfig", menuName = "Scopa/MAP Config", order = 1)]
    public class ScopaMapConfigAsset : ScriptableObject {
        public ScopaMapConfig config = new ScopaMapConfig();
    }

    [System.Serializable]
    public class ScopaMapConfig {
        [Header("Meshes")]
        [Tooltip("(default: 0.03125, 1 m = 32 units) The global scaling factor for all brush geometry and entity origins.")]
        public float scalingFactor = 0.03125f;

        [Tooltip("(default: true) Generate tangent data needed for normal mapping. If you're not using normal maps, disable for small memory savings.")]
        public bool addTangents = true;

        [Tooltip("(EDITOR-ONLY) (default: true) Generate lightmap UVs using Unity's built-in lightmap unwrapper.")]
        public bool addLightmapUV2 = true;

        [Tooltip("(EDITOR-ONLY) (default: Off) Use Unity's built-in mesh compressor. Reduces file size but may cause glitches and seams.")]
        public ModelImporterMeshCompression meshCompression = ModelImporterMeshCompression.Off;

        // TODO: merge brushes for each entity
        // TODO: remove unseen faces
        // TODO: vertex snapping

        [Tooltip("(default: sky, trigger, skip, hint, nodraw, null, clip) When a face's texture name is a partial match with this list, discard that face from the mesh; does not affect colliders")]
        public List<string> cullTextures = new List<string>() {"sky", "trigger", "skip", "hint", "nodraw", "null", "clip"};


        [Header("Colliders")]
        [Tooltip("(default: Box and Convex) For each brush we add a collider. Axis-aligned boxy brushes use Box Colliders, anything else gets a convex Mesh Collider. You can also force all Box / all Mesh colliders. For lots of brushes, a single merged concave Mesh Collider might be better.")]
        public ColliderImportMode colliderMode = ColliderImportMode.BoxAndConvex;

        [Tooltip("(default: illusionary) If an entity's classname is a partial match with this list, do not generate a collider for it.")]
        public List<string> nonsolidEntities = new List<string>() {"illusionary"};

        [Tooltip("(default: trigger) If an entity's classname is a partial match with this list, mark that collider as a non-solid trigger.")]
        public List<string> triggerEntities = new List<string>() {"trigger", "water"};


        [Header("Textures & Materials")]
        [Tooltip("(default: 128) To calculate texture coordinates, we need to know the texture image size; but if we can't find a matching texture, use this default size")]
        public int defaultTexSize = 128;

        [Tooltip("(EDITOR-ONLY) (default: true) try to automatically match each texture name to a similarly named Material already in the project; will only use manually set Materials below")]
        public bool findMaterials = true;

        [Tooltip("(optional) when we can't find a matching Material name, then use this default Material instead")]
        public Material defaultMaterial;

        [Tooltip("(optional) manually set a specific Material for each texture name")]
        public MaterialOverride[] materialOverrides;


        [Header("Entities")]
        [Tooltip("(optional) Prefab to use for every entity including 'worldspawn'. Colliders go here too. Useful for setting layers, static flags, etc.")]
        public GameObject entityPrefab;
        
        [Tooltip("(optional) Prefab to use for each mesh / material in each entity. meshFilter.sharedMesh and meshRenderer.sharedMaterial will be overridden. Useful for setting layers, renderer settings, etc.")]
        public GameObject meshPrefab;

        [Tooltip("(optional) For each entity type, you can set a different config. Useful for setting specific prefabs / mesh / collider settings.")]
        public ConfigOverride[] configOverrides;

        public bool IsTextureNameCulled(string textureName) {
            if ( string.IsNullOrWhiteSpace(textureName) )
                return true;

            var search = textureName.ToLowerInvariant();
            for(int i=0; i<cullTextures.Count; i++) {
                if ( search.Contains(cullTextures[i]) ) {
                    return true;
                }
            }
            return false;
        }

        public bool IsEntityNonsolid(string entityClassname) {
            var search = entityClassname.ToLowerInvariant();
            for(int i=0; i<nonsolidEntities.Count; i++) {
                if ( search.Contains(nonsolidEntities[i]) ) {
                    return true;
                }
            }
            return false;
        }

        public bool IsEntityTrigger(string entityClassname) {
            var search = entityClassname.ToLowerInvariant();
            for(int i=0; i<triggerEntities.Count; i++) {
                if ( search.Contains(triggerEntities[i]) ) {
                    return true;
                }
            }
            return false;
        }

        public Material GetMaterialOverrideFor(string textureName) {
            if ( materialOverrides.Length == 0) {
                return null;
            }

            var search = materialOverrides.Where( ov => textureName.ToLowerInvariant().Contains(ov.textureName.ToLowerInvariant()) ).FirstOrDefault();
            return search.material;
        }

        public ScopaMapConfig GetConfigOverrideFor(string entityClassname) {
            if ( configOverrides.Length == 0) {
                return null;
            }

            var search = configOverrides.Where( cfg => entityClassname.ToLowerInvariant().Contains(cfg.entityClassName.ToLowerInvariant()) ).FirstOrDefault();
            return search.configOverride.config;
        }

        [System.Serializable]
        public class ConfigOverride {
            [Tooltip("for example: func_detail, func_wall, light, etc... worldspawn is for world brushes. Partial matches count, e.g. 'func' will match all func_ entities.")]
            public string entityClassName;

            [SerializeReference] public ScopaMapConfigAsset configOverride;
        }

        [System.Serializable]
        public class MaterialOverride {
            [Tooltip("If a face has a texture name that matches this override, then use this Material no matter what. Partial matches count, e.g. an override for 'stone' will match all faces with texture names that contain the word 'stone'")]
            public string textureName;
            public Material material;
        }

        public enum ColliderImportMode {
            None,
            BoxColliderOnly,
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
    }

    
}

