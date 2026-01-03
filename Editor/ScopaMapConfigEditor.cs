using UnityEngine;
using UnityEditor;

namespace Scopa.Editor {

    [CustomPropertyDrawer(typeof(ScopaMapConfig))]
    public class ScopaMapConfigDrawer : PropertyDrawer
    {
        // Draw the property inside the given rect
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            // Using BeginProperty / EndProperty on the parent property means that
            // prefab override logic works on the entire property.
            EditorGUI.BeginProperty(position, label, property);

            // Draw label
            // position = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);

            // Don't make child fields be indented
            var indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;

            SerializedProperty prop = property.FindPropertyRelative("scalingFactor");
            do {
                // moved to LayerAttribute
                // if ( prop.name == "layer" ) {
                //     prop.intValue = EditorGUILayout.LayerField(new GUIContent(prop.displayName, prop.tooltip), prop.intValue );
                // } else {
                    EditorGUILayout.PropertyField(prop, true);
                // }
            }
            while (prop.NextVisible(false));

            if (GUILayout.Button( new GUIContent("open Project Settings > Scopa", "less common map import settings are in Project Settings > Scopa")))
                SettingsService.OpenProjectSettings("Project/Scopa");

            // Set indent back to what it was
            EditorGUI.indentLevel = indent;

            EditorGUI.EndProperty();
        }
    }
}