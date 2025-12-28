using UnityEngine;
using UnityEditor;

namespace Scopa.Editor {

    [CustomEditor(typeof(ScopaProjectSettings))]
    public class ScopaProjectSettingsEditor : UnityEditor.Editor 
    {
        public bool showWarning = true;
        public override void OnInspectorGUI() 
        {
            if (showWarning)
                EditorGUILayout.HelpBox("These are global project settings for Scopa. DON'T MOVE IT OR DELETE IT. You must keep this file at /Assets/Resources/Scopa/ScopaProjectSettings.asset so that Scopa can find it.", MessageType.Warning);
            
            DrawDefaultInspector();
        }
    }

}