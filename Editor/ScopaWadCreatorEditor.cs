using UnityEngine;
using UnityEditor;

namespace Scopa.Editor {

    [CustomPropertyDrawer(typeof(ScopaWadCreator))]
    public class ScopaWadCreatorDrawer : PropertyDrawer
    {
        Texture saveIcon;

        // Draw the property inside the given rect
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if ( saveIcon == null)
                saveIcon = EditorGUIUtility.IconContent("SaveActive").image;

            var wadCreator = property.GetSerializedValue<ScopaWadCreator>();

            if (GUI.Button(new Rect(position.x, position.y, position.width, 18), new GUIContent(" Export WAD...", saveIcon, "Generate and save a .WAD... We recommend choosing a folder OUTSIDE the Assets folder.")) ) {
                var defaultPath = ScopaCore.IsValidPath(wadCreator.lastSavePath) ? wadCreator.lastSavePath : Application.dataPath;
                var defaultFilename = ScopaCore.IsValidPath(wadCreator.lastSavePath) ? System.IO.Path.GetFileNameWithoutExtension(wadCreator.lastSavePath) : "New WAD";
                var newPath = EditorUtility.SaveFilePanel("Export WAD file to...", defaultPath, defaultFilename, "wad");
                if ( ScopaCore.IsValidPath(newPath) ) {
                    wadCreator.lastSavePath = newPath;
                    ScopaWad.SaveWad3File(newPath, wadCreator);
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

            SerializedProperty prop = property.FindPropertyRelative("resolution");
            do {
                EditorGUILayout.PropertyField(prop, true);
                if (prop.name == "resolution" && wadCreator.resolution == WadResolution.ProjectDefault && GUILayout.Button("edit default resolution in Project Settings > Scopa"))
                    SettingsService.OpenProjectSettings("Project/Scopa");
            }
            while (prop.NextVisible(false));

            // Set indent back to what it was
            EditorGUI.indentLevel = indent;

            EditorGUI.EndProperty();
        }
    }
}