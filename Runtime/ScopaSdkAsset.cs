using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Scopa {

    /// <summary>
    /// Generates a Trenchbroom level design SDK with GameConfig + FGD, as well as optional entity previews + texture WADs.
    /// </summary>
    [CreateAssetMenu(fileName = "New Scopa SDK", menuName = "Scopa/SDK Generator", order = -1)]
    public class ScopaSdkAsset : ScriptableObject
    {
        [Tooltip("Icon to display in TrenchBroom GameConfig menu. Will be exported to a 64x64 PNG.")]
        public Texture2D gameConfigIcon;

        [Tooltip("The default GameConfig is OK for most games, or at least at first.")]
        public ScopaGameConfig gameConfig;
  
        [Header("Entity Definitions (optional)")]
        public ScopaFgdConfigAsset fgd;

        [Header("Texture Sources (optional)")]
        [Tooltip("Distribute low-res 256 color versions of your textures, all in 1 convenient file.")]
        public ScopaWadCreatorAsset[] wads;
        // [Tooltip("If set, export .PNG equivalents of all MaterialOverrides")]
        // public ScopaMapConfigAsset[] materialOverrides;

        #if UNITY_EDITOR
        void OnEnable() {
            if (gameConfig != null)
                return;

            gameConfig = new(Application.productName);
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
        }
        #endif
    }

    [System.Serializable]
    /// <summary>
    /// This must match 1:1 with the latest TrenchBroom GameConfig file format, 
    /// since we're just using built-in JSON Utility to write out the file.
    /// see https://trenchbroom.github.io/manual/latest/#game_configuration_files
    /// Note that JSON is case-sensitive, for both keys and values.
    /// </summary>
    public class ScopaGameConfig {
        public int version = 9;
        public string name = "Unity (Scopa)";

        [Tooltip("don't edit this; it just changes the exported PNG icon file name")]
        public string icon = "Icon.png";

        [System.Serializable] public class FileFormats {
            public string format = "valve", initialmap = "initial_valve.map"; 
        }
        public FileFormats[] fileformats = { new() };

        [System.Serializable] public class FileSystem {
            public string searchpath = ".";

            [System.Serializable] public class PackageFormat {
                public string extension = "zip", format = "zip";
            }
            public PackageFormat packageformat = new();
        }
        public FileSystem filesystem = new();

        [System.Serializable] public class Materials {
            [Tooltip("root folder for where to look for textures")]
            public string root = "textures";

            [Tooltip("image file extensions, with special cases: .C means Half-Life WAD3")]
            public string[] extensions = { ".C", ".jpg", ".jpeg", ".tga", ".png" };

            [Tooltip("name of worldspawn property that stores WAD file paths")]
            public string attribute = "wad";

            [Tooltip("file patterns to ignore")]
            public string[] excludes = { "*_norm*", "*_gloss*", "*_spec*" };
        }
        public Materials materials = new();

        [System.Serializable] public class Entities {
            [Tooltip("default .FGD filepaths for Trenchbroom to try / suggest, relative to the Game Directory ")]
            public string[] definitions = { "Generic.fgd" };

            public Color defaultcolor = new Color(0.6f, 0.6f, 0.6f, 1.0f);

            [Tooltip("vectors or entity keynames for scaling models")]
            public string[] scale = {"modelscale", "modelscale_vec"};

            [Tooltip("if true, will initialize all entity values with default values; otherwise the value is omitted / empty")]
            public bool setDefaultProperties = false;
        }
        public Entities entities = new();

        [System.Serializable] public class Tags {
            [System.Serializable] public class TagGroup {
                [Tooltip("The name of the group, as displayed in TB's View Options")]
                public string name = "Trigger";

                [Tooltip("valid render attributes: transparent")]
                public string[] attribs = {"transparent"};

                [Tooltip("valid match modes: classname, material")]
                public string match = "classname";

                [Tooltip("search patterns can have * wildcards")]
                public string pattern = "trigger*";

                public TagGroup() {}

                public TagGroup(string name, string match, string pattern, params string[] attribs) {
                    this.name = name;
                    this.match = match;
                    this.pattern = pattern;
                    this.attribs = attribs;
                }
            }
            [Tooltip("To filter view via TB's View Options. Match by 'classname'")]
            public TagGroup[] brush = { 
                new("Detail", "classname", "func_detail*"),
                new("Trigger", "classname", "trigger*", "transparent")
            };
            [Tooltip("To filter view via TB's View Options. Match by 'material'")]
            public TagGroup[] brushface = { 
                new("Skip", "material", "skip*"),
                new("Clip", "material", "clip*", "transparent")
            };
        }
        public Tags tags = new();

        // NOTE: We intentionally do not support Quake 2 surface flags or faceattribs
        // and anyway it's a bad way to mark up surfaces, with no native Unity equivalent.

        public string softMapBounds = "-32768 -32768 -32768 32768 32768 32768";

        [System.Serializable] public class CompilationTool {
            public string name = "tool name";
            public string description = "Path to the ___ tool, which does some kind of map compile or baking operation.";
        }
        public CompilationTool[] compilationTools;

        public ScopaGameConfig() {}

        public ScopaGameConfig (string projectName) {
            this.name = projectName;
        }

        public override string ToString() {
            return JsonUtility.ToJson(this, true);
        }
    }

}
