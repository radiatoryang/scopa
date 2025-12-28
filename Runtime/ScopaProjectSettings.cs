using UnityEngine;
using System.Collections;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Scopa {
    /// <summary>
    /// Global project settings for Scopa, saved in /Assets/Resources/ so accessible at runtime if needed.
    /// Access via ScopaProjectSettings.Get() at runtime or editor time, or use Recache() + GetCached() for performance-critical stuff
    /// </summary>
    public class ScopaProjectSettings : ScriptableObject {
        [HelpBox("Each .MAP can have its own config, but this gets hard to maintain with more maps. We strongly recommend making an external MAP config asset.", HelpBoxMessageType.Info)]
        public ScopaMapConfigAsset defaultMapImportConfig;

        public enum TexelResolution {
            Full = 1,
            Half = 2,
            Quarter = 4,
            Eighth = 8,
            Sixteenth = 16
        }

        [Header("Textures & Materials")]
        [Tooltip("(default: Full) global scaling for all UV coordinates; if you're mapping in your editor with full resolution textures, leave this at Full... but if you're using WADs, we recommend Quarter resolution")]
        public TexelResolution editorTextureResolution = TexelResolution.Full;

        [Tooltip("(default: 128) If we can't match a map face to a Material's Main Texture, use this default placeholder texture size. Half-Life 1 used 128, Half-Life 2 used 512.")]
        public int defaultTexSize = 128;

        [Tooltip("(optional) If we can't match a map face to a Material, then use this default placeholder material instead. If you're hunting for broken missing textures, maybe set this to a bright neon pink checkerboard texture or something.")]
        public Material defaultMaterial;
        

        [Header("Quake / Half-Life keyword adapter")]
        [HelpBox("Use keywords to adapt Quake / Half-Life maps with minimal FGD config or prefab setup. If you ignore Quake / Half-Life naming conventions, you may want to delete some of these. If in doubt, it's safer to just leave it all as-is, since TrenchBroom expects these conventions.", HelpBoxMessageType.Info)]
        
        [Tooltip("(default: sky, trigger, skip, hint, nodraw, null, clip, origin) When a face's texture name contains any word in this list, discard that face from the mesh. But this does not affect mesh colliders.")]
        public List<string> cullTextures = new List<string>() {"sky", "trigger", "skip", "hint", "nodraw", "null", "clip", "origin"};

        [Tooltip("(default: illusionary) If an entity's classname contains a word in this list, do not generate a collider for it and disable Navigation Static for it.")]
        public List<string> nonsolidEntities = new List<string>() {"illusionary"};

        [Tooltip("(default: trigger, water) If an entity's classname contains a word in this list, mark that collider as a non-solid trigger and disable Navigation Static for it.")]
        public List<string> triggerEntities = new List<string>() {"trigger", "water"};

        [Tooltip("(default: func_group, func_detail) If an entity classname contains any word in this list, then merge its brushes (mesh and collider) into worldspawn and discard entity data. WARNING: most per-entity mesh and collider configs will be overriden by worldspawn; only the discarded entity's solidity will be respected.")]
        public List<string> mergeToWorld = new List<string>() {"func_group", "func_detail"};

        [Tooltip("(default: worldspawn, func_wall) If an entity classname contains any word in this list AND it doesn't have prefab overrides (see Entity Overrides), then set its mesh objects to be static -- batching, lightmapping, navigation, reflection, everything. However, non-solid and trigger entities will NOT be navigation static.")]
        public List<string> staticEntities = new List<string>() {"worldspawn", "func_wall"};

        [Header("Scopa Entity Scripting")]
        [HelpBox("If you want to use some of Scopa's built-in entity handling / API, this is where you configure it -- or where you turn it off.")]
        [Tooltip("(default: true) if enabled, automatically add ScopaEntity component to all game objects (if not already present in the entityPrefab)... disable this if you don't want to use the built-in ScopaEntity at all, and override it with your own")]
        public bool addScopaEntityComponent = true;

        [Tooltip("(default: true) if enabled, will call OnEntityImport on any script that implements IScopaEntityImport")]
        public bool callOnEntityImport = true;


        // boring file handling stuff goes below
        static ScopaProjectSettings cachedRuntimeSettings;
        public static string SettingsPath { get { return SettingsFolder + "/" + SettingsFilename; } }
        public static string SettingsFolder = "Scopa";
        public static string SettingsFilename = "ScopaProjectSettings.asset";

        public static ScopaProjectSettings Get() {
            try {
                #if UNITY_EDITOR
                
                // EDITOR BEHAVIOR: freshly load the ScriptableObject from Resources, or create if it doesn't exist yet
                return GetOrCreateSettings();

                #else

                // RUNTIME BEHAVIOR: cache the settings, and use default settings if none are found
                if (cachedRuntimeSettings == null)
                    Recache();
                return GetCached();

                #endif
                    
            } catch (System.Exception e) {
                Debug.LogError($"Failed to load Scopa project settings at {SettingsPath}: {e.Message}");
                return null;
            }
        }

        public static void Recache() {
            cachedRuntimeSettings = Resources.Load<ScopaProjectSettings>(SettingsPath);
            
            if (cachedRuntimeSettings == null) {
                #if UNITY_EDITOR
                cachedRuntimeSettings = GetOrCreateSettings();
                #else
                cachedRuntimeSettings = CreateInstance<ScopaProjectSettings>();
                #endif
            }
        }
 
        public static ScopaProjectSettings GetCached() {
            if (cachedRuntimeSettings == null)
                Recache();
            return cachedRuntimeSettings;
        }

        #if UNITY_EDITOR
        static ScopaProjectSettings GetOrCreateSettings() {
            var settings = AssetDatabase.LoadAssetAtPath<ScopaProjectSettings>("Assets/Resources/" + SettingsPath);
            if (settings == null) {
                if (!AssetDatabase.IsValidFolder("Assets/Resources/" + SettingsFolder))
                    AssetDatabase.CreateFolder("Assets/Resources", SettingsFolder);

                settings = CreateInstance<ScopaProjectSettings>();
                AssetDatabase.CreateAsset(settings, "Assets/Resources/" + SettingsPath);
                AssetDatabase.SaveAssets();
            }
            return settings;
        }
        #endif
    }
}