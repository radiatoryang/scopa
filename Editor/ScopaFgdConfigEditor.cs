using UnityEngine;
using UnityEditor;

namespace Scopa.Editor {

    [CustomPropertyDrawer(typeof(ScopaFgdConfig))]
    public class ScopaFgdConfigDrawer : PropertyDrawer
    {
        // Draw the property inside the given rect
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            // Using BeginProperty / EndProperty on the parent property means that
            // prefab override logic works on the entire property.
            EditorGUI.BeginProperty(position, label, property);

            if (GUILayout.Button("test export to editor console") ) {
                Debug.Log(property.GetSerializedValue<ScopaFgdConfig>().ToString());
            }

            // Draw label
            // position = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);

            // Don't make child fields be indented
            var indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;

            SerializedProperty prop = property.FindPropertyRelative("worldspawn");
            do {
                EditorGUILayout.PropertyField(prop, true);
            }
            while (prop.NextVisible(false));

            // Set indent back to what it was
            EditorGUI.indentLevel = indent;

            EditorGUI.EndProperty();
        }
    }
}