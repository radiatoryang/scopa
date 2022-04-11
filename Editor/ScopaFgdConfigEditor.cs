using UnityEngine;
using UnityEditor;

namespace Scopa.Editor {

    [CustomPropertyDrawer(typeof(ScopaFgdConfig))]
    public class ScopaFgdConfigDrawer : PropertyDrawer
    {
        Texture saveIcon;

        // Draw the property inside the given rect
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if ( saveIcon == null)
                saveIcon = EditorGUIUtility.IconContent("SaveActive").image;

            if (GUI.Button(new Rect(position.x, position.y, position.width, 18), new GUIContent(" Export FGD...", saveIcon, "Generate and save a .FGD file in UTF-8 encoding. We recommend choosing a folder OUTSIDE the Assets folder.")) ) {
                var fgd = property.GetSerializedValue<ScopaFgdConfig>();
                var defaultPath = ScopaCore.IsValidPath(fgd.lastSavePath) ? fgd.lastSavePath : Application.dataPath;
                var defaultFilename = ScopaCore.IsValidPath(fgd.lastSavePath) ? System.IO.Path.GetFileNameWithoutExtension(fgd.lastSavePath) : "New FGD";
                var newPath = EditorUtility.SaveFilePanel("Export FGD file to...", defaultPath, defaultFilename, "fgd");
                if ( ScopaCore.IsValidPath(newPath) ) {
                    fgd.lastSavePath = newPath;
                    ScopaCore.ExportFgdFile(fgd, newPath);
                    property.serializedObject.ApplyModifiedProperties();
                }
                GUIUtility.ExitGUI();
                return;
            }

            // Using BeginProperty / EndProperty on the parent property means that
            // prefab override logic works on the entire property.
            EditorGUI.BeginProperty(position, label, property);

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