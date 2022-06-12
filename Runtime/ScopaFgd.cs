using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using Mesh = UnityEngine.Mesh;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Scopa {

    /// <summary> magically binds this public variable to an FGD *and* populates it with entity data on import 
    /// and, if it has a [Tooltip] attribute, it also pulls that into the FGD as help text! wow! 
    /// ... BUT, VERY IMPORTANT: this works only if the MonoBehaviour implements IScopaEntity!!! </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class FgdVar : Attribute {
        public string propertyKey, editorLabel;
        public VarType propertyType;

        public FgdVar( string propertyName, VarType propertyType ) {
            this.propertyKey = propertyName;
            this.editorLabel = propertyName;
            this.propertyType = propertyType;
        }

        public FgdVar( string propertyName, VarType propertyType, string editorLabel ) {
            this.propertyKey = propertyName;
            this.editorLabel = editorLabel;
            this.propertyType = propertyType;
        }

        public enum VarType {
            Float,
            Vector3Scaled
        }
    }

    /// <summary>main class for core Scopa FGD functions</summary>
    public static class ScopaFgd {
        public static void ExportFgdFile(ScopaFgdConfig fgd, string filepath) {
            var fgdText = fgd.ToString();
            var encoding = new System.Text.UTF8Encoding(false); // no BOM
            System.IO.File.WriteAllText(filepath, fgdText, encoding);
            Debug.Log("wrote FGD to " + filepath);

            ExportObjModels(fgd, filepath);
            Debug.Log("wrote OBJs to " + filepath);
        }

        public static void ExportObjModels(ScopaFgdConfig fgd, string filepath) {
            var folder = Path.GetDirectoryName(filepath) + "/assets/";

            // TODO: create folder if it doesn't exist

            foreach( var entity in fgd.entityTypes ) {
                if ( entity.objScale > 0)
                    ObjExport.SaveObjFile( folder + entity.className + ".obj", entity.entityPrefab, Vector3.one * entity.objScale);
            }
        }

    }
}