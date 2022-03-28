using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace Scopa {
    /// <summary> ScriptableObject to use for configuring how Scopa imports .MAPs, even for runtime imports too. </summary>
    [CreateAssetMenu(fileName = "New ScopaMapConfig", menuName = "Scopa/MAP Config", order = 1)]
    public class ScopaMapConfigAsset : ScriptableObject {
        

    }

    [System.Serializable]
    public class ScopaMapConfig {
        [Tooltip("(default: 0.03125, 1 m = 32 units) The global scaling factor for all brush geometry and entity origins.")]
        public float scalingFactor = 0.03125f;

        // TODO: merge brushes for each entity
        // TODO: remove unseen faces
        // TODO: vertex snapping

        [Tooltip("(optional) For each entity type (including 'worldspawn'), set a prefab to use. Add a ScopaEntity component to access entity properties and more.")]
        public PrefabOverride[] prefabOverrides;

        [Tooltip("(default: Both) For each brush, we generate a collider. Axis-aligned boxes use Box Colliders, anything else gets a convex Mesh Collider. You can also force all Box / all Mesh colliders.")]
        public ColliderImportMode colliderMode = ColliderImportMode.Both;


        [Header("Textures & Materials")]
        [Tooltip("(default: 128) To calculate texture coordinates, we need to know the texture image size; but if we can't find a matching texture, use this default size")]
        public int defaultTexSize = 128;

        [Tooltip("(default: true) try to automatically match each texture name to a similarly named Material already in the project")]
        public bool findMaterials = true;

        [Tooltip("(optional) when we can't find a matching Material name, then use this default Material instead")]
        public Material defaultMaterial;

        [Tooltip("(optional) manually set a specific Material for each texture name")]
        public MaterialOverride[] materialOverrides;

        [System.Serializable]
        public class PrefabOverride {
            [Tooltip("for example: func_detail, func_wall, light, etc... worldspawn is for world brushes")]
            public string entityClassName;

            [Tooltip("template for the main root object of the entity, where we add colliders\n - to access entity data, add a ScopaEntity component\n - to fine-tune import settings, add a ScopaConfig")]
            public GameObject entityPrefab;

            [Tooltip("template for each mesh renderer of the entity, if any\n - to fine-tune import settings just for these brushes, put a ScopaConfig component on the prefab")]
            public GameObject brushPrefab;
        }

        [System.Serializable]
        public class MaterialOverride {
            public string textureName;
            public Material material;
        }

        public enum ColliderImportMode {
            None,
            BoxColliderOnly,
            ConvexMeshColliderOnly,
            Both
        }
    }

    
}

