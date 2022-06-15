using System;
using UnityEngine;
using UnityEditor;
#if UNITY_2020_2_OR_NEWER
using UnityEditor.AssetImporters;
#elif UNITY_2017_1_OR_NEWER
using UnityEditor.Experimental.AssetImporters;
#endif

using System.Linq;

namespace Scopa.Editor {

    [CustomEditor(typeof(MapImporter))]
    public class MapImporterEditor: ScriptedImporterEditor
    {
        // UnityEditor.Editor wadConfigEditor;
        // SerializedProperty editedCopy;

        // public override void OnDisable()
        // {
        //     if ( wadConfigEditor != null)
        //         DestroyImmediate( wadConfigEditor );
        //     base.OnDisable();
        // }

        const int EXTERNAL_CONFIG_FIELD_OFFSET = 320;
        const int SAVE_BUTTON_WIDTH = 96;
        bool wasModified = false;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUI.LabelField( new Rect(16, 0, Screen.width, 32), ".MAP Import Settings", EditorStyles.boldLabel);

            var externalConfig = serializedObject.FindProperty("externalConfig");
            EditorGUI.PropertyField(new Rect(EXTERNAL_CONFIG_FIELD_OFFSET, 8, Screen.width-EXTERNAL_CONFIG_FIELD_OFFSET-16, 20), externalConfig, new GUIContent() );

            var internalConfig = serializedObject.FindProperty("config");

            if ( GUI.Button( new Rect(EXTERNAL_CONFIG_FIELD_OFFSET-SAVE_BUTTON_WIDTH, 8, SAVE_BUTTON_WIDTH, 20), "Save as Asset...") ) {
                var newPath = EditorUtility.SaveFilePanelInProject("Export import settings as Map Config Asset...", "New Map Config Asset", "asset", "Save these MAP importer settings as an external asset, somewhere in your Assets folder.");
                var configAsset = ScriptableObject.CreateInstance<ScopaMapConfigAsset>();
                configAsset.config = internalConfig.GetSerializedValue<ScopaMapConfig>().ShallowCopy();
                AssetDatabase.CreateAsset(configAsset, newPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                EditorUtility.FocusProjectWindow ();
                externalConfig.objectReferenceInstanceIDValue = configAsset.GetInstanceID();
                Selection.activeObject = configAsset;
            }

            SerializedObject externalConfigObj = null;
            if (externalConfig.objectReferenceValue != null) {
                externalConfigObj = new SerializedObject(externalConfig.objectReferenceValue);
            }
            
            EditorGUILayout.PropertyField( externalConfigObj != null ? externalConfigObj.FindProperty("config") : internalConfig);

            if ( externalConfigObj != null ) {
                bool modifiedExternal = externalConfigObj.ApplyModifiedProperties();
                if ( modifiedExternal ) {
                    wasModified = true;
                }
            }

            serializedObject.ApplyModifiedProperties();
            base.ApplyRevertGUI();

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

        protected override void ResetValues()
        {
            wasModified = false;
            base.ResetValues();
        }


    }

}