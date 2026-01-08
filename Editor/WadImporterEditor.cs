using UnityEngine;
using UnityEditor;
#if UNITY_2020_2_OR_NEWER
using UnityEditor.AssetImporters;
#elif UNITY_2017_1_OR_NEWER
using UnityEditor.Experimental.AssetImporters;
#endif

using System.Linq;

namespace Scopa.Editor {

    [CustomEditor(typeof(WadImporter))]
    public class WadImporterEditor: ScriptedImporterEditor
    {
        UnityEditor.Editor wadConfigEditor;
        // SerializedProperty editedCopy;
        bool wasModified = false;
        bool showTextures = false;
        bool showMaterials = false;

        public override void OnDisable()
        {
            if ( wadConfigEditor != null)
                DestroyImmediate( wadConfigEditor );
            base.OnDisable();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUI.LabelField( new Rect(16, 0, Screen.width, 32), ".WAD Import Settings", EditorStyles.boldLabel);
        
            var wadConfig = serializedObject.FindProperty("config");
            EditorGUILayout.PropertyField(wadConfig);

            // var wadConfigOverride = serializedObject.FindProperty("configOverride");
            // EditorGUILayout.PropertyField(wadConfigOverride, new GUIContent("WAD Config Override", "to set WAD import settings from an external asset, then set this override"));

            // initialize Wad Config inspector, destroy if unused
            // if ( wadConfig.objectReferenceValue != null && wadConfigEditor == null) {
            //     Debug.Log("init?");
            //     editedCopy = wadConfigOverride.objectReferenceValue != null ? wadConfigOverride.Copy() : wadConfig.Copy();
            //     wadConfigEditor = UnityEditor.Editor.CreateEditor( editedCopy.objectReferenceValue as ScopaWadConfig );
            // }

            // if ( wadConfig.objectReferenceValue == null && wadConfigEditor != null) {
            //     DestroyImmediate( wadConfigEditor );
            // }

            // if ( wadConfigEditor != null) {
            //     EditorGUI.BeginChangeCheck();
            //     wadConfigEditor.OnInspectorGUI();
            //     if ( EditorGUI.EndChangeCheck() ) {
            //         Debug.Log("changed!");
            //         wasModified = true;
            //     }
            // }

            serializedObject.ApplyModifiedProperties();
            base.ApplyRevertGUI();

            // list read-only list of imported textures
            var textures = AssetDatabase.LoadAllAssetRepresentationsAtPath( (target as WadImporter).assetPath ).Where( obj => obj is Texture2D);
            var textureCount = textures.Count();

            showTextures = EditorGUILayout.Foldout(showTextures, $"Generated Textures ({textureCount})");
            if ( showTextures ) {
                GUI.enabled = false;
                foreach (var tex in textures) {
                    EditorGUILayout.ObjectField(tex, typeof(Texture2D), false);
                }
                GUI.enabled = true;
            }

            // list read-only list of imported materials
            var materials = AssetDatabase.LoadAllAssetRepresentationsAtPath( (target as WadImporter).assetPath ).Where( obj => obj is Material);
            var materialCount = materials.Count();

            showMaterials = EditorGUILayout.Foldout(showMaterials, $"Generated Materials ({materialCount})");
            if ( showMaterials ) {
                GUI.enabled = false;
                foreach (var mat in materials) {
                    EditorGUILayout.ObjectField(mat, typeof(Material), false);
                }
                GUI.enabled = true;
            }

        }

        public override bool HasModified()
        {
            return wasModified || base.HasModified();
        }

        protected override void Apply()
        {
            wasModified = false;
            // serializedObject.CopyFromSerializedPropertyIfDifferent(editedCopy);
            base.Apply();
        }

        public override void DiscardChanges()
        {
            wasModified = false;
            base.DiscardChanges();
        }


    }

}