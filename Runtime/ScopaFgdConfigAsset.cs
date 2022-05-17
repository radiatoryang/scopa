using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Scopa {
    /// <summary> ScriptableObject to use for configuring how Scopa exports .FGDs / setup entity bindings. </summary>
    [CreateAssetMenu(fileName = "New ScopaFgdConfig", menuName = "Scopa/FGD Config Asset", order = 1)]
    public class ScopaFgdConfigAsset : ScriptableObject {
        public ScopaFgdConfig config = new ScopaFgdConfig();
    }

    [System.Serializable]
    public class ScopaFgdConfig {       
        [Tooltip("worldspawn is a required brush entity type, the root of all entities")]
        public FgdClass worldspawn = new FgdClass("worldspawn", FgdClassType.SolidClass);

        [Tooltip("Inlines another FGD's data into this FGD. Only *this* FGD's worldspawn and includes will be used... it won't include the include's includes.")]
        [SerializeReference] public ScopaFgdConfigAsset[] includeFgds;

        [Tooltip("define additional entity types here, e.g. func_wall, info_player_start, monster_zombie")]
        public FgdClass[] entityTypes;

        [Tooltip("a base is a reusable chunk of an entity definition, think of it as includes / templates you can mix and match")]
        public FgdClassBase[] entityBases;

        [HideInInspector] public string lastSavePath;

        public string ToString(bool includeHeaderAndWorldspawnAndIncludes = true) {
            var text = "";

            if ( includeHeaderAndWorldspawnAndIncludes ) {
                text += "\n// ======================================================================";
                text += "\n// FGD for " + Application.productName + " " + Application.version;
                text += "\n// generated on " + System.DateTime.Now.ToString("f");
                text += "\n// ======================================================================\n\n";
            }

            text += string.Join("\n\n", entityBases.Select( ent => ent.ToString() ) ) + "\n\n";

            if ( includeHeaderAndWorldspawnAndIncludes ) {
                text += worldspawn.ToString() + "\n\n";

                foreach ( var include in includeFgds ) {
                    text += include.config.ToString(false);
                }
            }

            text += string.Join("\n\n", entityTypes.Select( ent => ent.ToString() ) ) + "\n\n";

            return text;
        }

        /// <summary> utility function to collect all defined entity types together, as well as all the entities defined in any FGD includes </summary>
        List<FgdClass> GetAllEntityTypesWithIncludes() {
            var allEntityTypes = new List<FgdClass>( entityTypes );
            foreach ( var include in includeFgds ) {
                allEntityTypes.AddRange( include.config.entityTypes );
            }
            return allEntityTypes;
        }

        /// <summary> returns null if no entityPrefab defined; note: entityClassname must already be ToLowerInvariant() and match exactly </summary>
        public GameObject GetEntityPrefabFor(string entityClassname) {
            var search = GetAllEntityTypesWithIncludes().Where( cfg => entityClassname == cfg.className.ToLowerInvariant() ).FirstOrDefault();
            if ( search != null && search.entityPrefab != null) {
                return search.entityPrefab;
            }
            return null;
        }

        /// <summary> returns null if no meshPrefab found; note: entityClassname must already be ToLowerInvariant() and match exactly </summary>
        public GameObject GetMeshPrefabFor(string entityClassname) {
            var search = GetAllEntityTypesWithIncludes().Where( cfg => entityClassname == cfg.className.ToLowerInvariant() ).FirstOrDefault();
            if ( search != null && search.meshPrefab != null) {
                return search.meshPrefab;
            }
            return null;
        }

        [System.Serializable]
        public class FgdClassBase {
            [Tooltip("(REQUIRED) Name of base class to define reusable properties for other entities; cannot be used as an actual entity, so don't name it like one")]
            public string baseName = "new_base";

            [Tooltip("(optional) a base can include other bases... but beware of recursion, don't include a base that includes itself, that would be bad")] 
            public string[] baseIncludes;

            [Tooltip("reusable set of properties defined in the base class")]
            public FgdProperty[] properties;

            public override string ToString() {
                var text = $"@BaseClass ";
                
                if ( baseIncludes.Length > 0) {
                    text += $"base({string.Join(", ", baseIncludes)}) ";
                }

                text += $" = {baseName}\n[\n {string.Join("\n", properties.Select( prop => prop.ToString() ))}\n]";

                return text;
            }
        }

        [System.Serializable]
        public class FgdClass {
            [Tooltip("(REQUIRED) Full classname, e.g. func_wall, info_player_start")]
            public string className = "new_entity";

            [Tooltip("note that Quake / Half-Life 1 TrenchBroom only supports PointClass or SolidClass (brush entity)")]
            public FgdClassType classType = FgdClassType.PointClass;

            [Tooltip("size of the object in map units, in the level editor; set size to 0, 0, 0 to omit")]
            public Bounds editorSize = new Bounds( Vector3.zero, Vector3.one * 32);

            [Tooltip("color of the object in the level editor; set alpha to 0 to omit")]
            public Color32 editorColor = new Color32(0xff, 0, 0, 0xff);

            [Tooltip("(optional) description / advice / tips displayed only in the level editor")]
            public string editorHelp;

            [Tooltip("(optional) custom prefab templates to use, just for this entity type")]
            public GameObject entityPrefab, meshPrefab;

            [Tooltip("if exporting an OBJ preview, should we scale the model? Set to <= 0 to disable OBJ generation for this entity.")]
            public float objScale = 0f;

            [Tooltip("(optional) base property templates to include")] 
            public string[] baseIncludes;

            public FgdProperty[] properties;

            public FgdClass(string classname, FgdClassType classtype) {
                this.className = classname;
                this.classType = classtype;
            }

            public override string ToString() {
                // TODO: use StringBuilder

                var text = $"@{classType.ToString()} ";

                // if ( classType == FgdClassType.PointClass) {
                //     text += "flags(Angle) ";
                // }
                
                if ( baseIncludes.Length > 0) {
                    text += $"base({string.Join(", ", baseIncludes)}) ";
                }

                if ( classType == FgdClassType.PointClass && editorSize.size.sqrMagnitude > 0 ) {
                    text += $"size({editorSize.min.ToStringIntWithNoPunctuation()}, {editorSize.max.ToStringIntWithNoPunctuation()}) ";
                }

                if ( editorColor.a != 0x00 ) {
                    text += $"color({editorColor.r} {editorColor.g} {editorColor.b}) ";
                }

                if ( classType == FgdClassType.PointClass && entityPrefab != null && objScale > 0 ) {
                    text += $"model(\"assets/{className}.obj\") ";
                }

                text += $"= {className} : \"{editorHelp}\"\n[\n{string.Join("\n", properties.Select( prop => prop.ToString() ))}\n]";

                return text;
            }
        }

        public enum FgdClassType {
            PointClass,
            SolidClass
        }

        [System.Serializable]
        public class FgdProperty {
            [Tooltip("the internal key name used in the .MAP data")]
            public string key = "new_key";

            [Tooltip("the property type, affects how it's displayed in the level editor")]
            public FgdPropertyType type = FgdPropertyType.String;
            
            [Tooltip("the 'nice name' displayed only in the level editor")]
            public string editorLabel = "New Key";

            [Tooltip("(optional) description / advice / tips displayed only in the level editor")]
            public string editorHelp;

            [Tooltip("(optional) default value initialized on the entity in the editor, if any?\n - for Choices type, type in the number index\n - for Flags type, toggle on the each flag entry")]
            public string defaultValue;

            [Tooltip("define the possible enum choices here")]
            public FgdEnum[] choices;

            [Tooltip("define the bitwise boolean spawnflags here")]
            public FgdFlag[] flags;

            public override string ToString() {
                var text = $"    {key}({type.ToString().ToLowerInvariant()})";

                if ( type != FgdPropertyType.Flags ) {
                    text += $": \"{editorLabel}\" : \"{defaultValue}\" : \"{editorHelp}\"";
                }

                if ( type == FgdPropertyType.Choices ) {
                    text += $" =\n    [\n        { string.Join("\n        ", choices.Select( (choice, index) => choice.ToString(index)) ) } \n    ]";
                } else if ( type == FgdPropertyType.Flags ) {
                    text += $" =\n    [\n        { string.Join("\n        ", flags.Select( (flag, index) => flag.ToString(index)) ) } \n    ]";
                }

                return text;
            }
        }

        public enum FgdPropertyType {
            String,
            Integer,
            Choices,
            Flags
        }

        [System.Serializable]
        public class FgdEnum {
            [Tooltip("the 'nice name' displayed only in the level editor")]
            public string label = "New Choice";

            [Tooltip("actual number index corresponding for each choice... e.g. 0, or 1, or 2, etc.")]
            public int value = 0;

            public string ToString(int index) {
                return $"{index} : \"{label}\"";
            }
        }

        [System.Serializable]
        public class FgdFlag {
            [Tooltip("default value for this flag")]
            public bool defaultValue = false;

            [Tooltip("the 'nice name' displayed only in the level editor")]
            public string label = "New Flag";

            [Tooltip("if Use Order In List, flag #1 will be 1, flag #2 will be 2, etc ... or you can manually override the bit value")]
            public FgdFlagBit bitValue = FgdFlagBit.UseOrderInList;

            public string ToString(int index) {
                if (bitValue != FgdFlagBit.UseOrderInList) {
                    index = int.Parse( bitValue.ToString().Substring(1) );
                } else {
                    index = Mathf.ClosestPowerOfTwo( Mathf.RoundToInt(Mathf.Pow(2, index)) );
                }

                return $"{index} : \"{label}\" : { (defaultValue ? "1" : "0") }";
            }
        }

        public enum FgdFlagBit {
            UseOrderInList, 
            _1, _2, _4, _8, _16, _32, _64, _128,
            _256, _512, _1024, _2048, _8192, _16384, _32768,
            _65536, _131072, _262144, _524288, _1048576, _2097152, _4194304, _8388608
        }

    }

    
}

