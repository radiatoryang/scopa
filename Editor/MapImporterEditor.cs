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

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUI.LabelField( new Rect(16, 0, Screen.width, 32), ".MAP Import Settings", EditorStyles.boldLabel);
        
            var mapConfig = serializedObject.FindProperty("config");
            EditorGUILayout.PropertyField(mapConfig);

            serializedObject.ApplyModifiedProperties();
            base.ApplyRevertGUI();

        }

        // public override bool HasModified()
        // {
        //     return wasModified || base.HasModified();
        // }

        // protected override void Apply()
        // {
        //     wasModified = false;
        //     // serializedObject.CopyFromSerializedPropertyIfDifferent(editedCopy);
        //     base.Apply();
        // }

        // protected override void ResetValues()
        // {
        //     wasModified = false;
        //     base.ResetValues();
        // }


    }

}