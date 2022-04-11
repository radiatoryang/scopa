using UnityEngine;
using UnityEditor;

namespace Scopa.Editor {

    [CustomPropertyDrawer(typeof(ScopaFgdConfig.FgdClass))]
    public class ScopaFgdClassDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if ( property.isExpanded ) {
                return EditorGUI.GetPropertyHeight(property) - 20;
            } 
            return base.GetPropertyHeight(property, label);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            // Using BeginProperty / EndProperty on the parent property means that
            // prefab override logic works on the entire property.
            EditorGUI.BeginProperty(position, label, property);

            // Draw label
            property.isExpanded = EditorGUI.Foldout( new Rect(position.x, position.y, position.width, 18), property.isExpanded, label);

            if ( property.isExpanded ) {
                var fgd = property.GetSerializedValue<ScopaFgdConfig.FgdClass>();
                var drawPos = new Rect(position);
                drawPos.y += 20;
                EditorGUI.indentLevel++;

                SerializedProperty prop = property.FindPropertyRelative("className");
                do {
                    if ( fgd.classType == ScopaFgdConfig.FgdClassType.SolidClass && prop.name == "editorSize") {
                        continue;
                    }
                    if ( fgd.classType == ScopaFgdConfig.FgdClassType.PointClass && prop.name == "meshPrefab") {
                        continue;
                    }

                    drawPos.height = EditorGUI.GetPropertyHeight(prop);
                    EditorGUI.PropertyField(drawPos, prop, true);
                    drawPos.y += drawPos.height + 2;
                }
                while (prop.NextVisible(false) && prop.type != "FgdClass" && prop.type != "FgdClassBase" && prop.type != "ScopaFgdConfigAsset"  && prop.type != "ScopaFgdConfig" && prop.name != "includeFgds");

                EditorGUI.indentLevel--;
            }

            EditorGUI.EndProperty();
            // EditorUtility.SetDirty(property.serializedObject.targetObject);
        }
    }

    [CustomPropertyDrawer(typeof(ScopaFgdConfig.FgdProperty))]
    public class ScopaFgdPropertyDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if ( property.isExpanded ) {
                return EditorGUI.GetPropertyHeight(property) - 20;
            } 
            return base.GetPropertyHeight(property, label);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            // Using BeginProperty / EndProperty on the parent property means that
            // prefab override logic works on the entire property.
            EditorGUI.BeginProperty(position, label, property);

            // Draw label
            property.isExpanded = EditorGUI.Foldout( new Rect(position.x, position.y, position.width, 18), property.isExpanded, label);

            if ( property.isExpanded ) {
                var fgd = property.GetSerializedValue<ScopaFgdConfig.FgdProperty>();
                var drawPos = new Rect(position);
                drawPos.y += 20;
                EditorGUI.indentLevel++;

                SerializedProperty prop = property.FindPropertyRelative("key");
                do {
                    if ( fgd.type != ScopaFgdConfig.FgdPropertyType.Choices && prop.name == "choices") {
                        prop.isExpanded = false;
                        continue;
                    }
                    if ( fgd.type != ScopaFgdConfig.FgdPropertyType.Flags && prop.name == "flags") {
                        prop.isExpanded = false;
                        continue;
                    }
                    if ( fgd.type == ScopaFgdConfig.FgdPropertyType.Flags && prop.name == "defaultValue") {
                        continue;
                    }

                    drawPos.height = EditorGUI.GetPropertyHeight(prop);
                    EditorGUI.PropertyField(drawPos, prop, true);
                    drawPos.y += drawPos.height + 2;

                    if ( fgd.type == ScopaFgdConfig.FgdPropertyType.Flags && prop.name == "flags" && fgd.key != "spawnflags" ) {
                        EditorGUI.HelpBox( drawPos, "ERROR: all properties of type Flags *MUST* have key = 'spawnflags'", MessageType.Error );
                    }
                }
                while (prop.NextVisible(false) && prop.type != "FgdProperty" && prop.type != "FgdClassBase" && prop.type != "ScopaFgdConfigAsset"  && prop.type != "ScopaFgdConfig" && prop.name != "includeFgds");

                EditorGUI.indentLevel--;
            }

            EditorGUI.EndProperty();
            // EditorUtility.SetDirty(property.serializedObject.targetObject);
        }
    }

    [CustomPropertyDrawer(typeof(ScopaFgdConfig.FgdEnum))]
    public class ScopaFgdEnumDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return base.GetPropertyHeight(property, label);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var drawPos = new Rect(position);
            drawPos.width = drawPos.width / 2 - 8;

            SerializedProperty prop = property.FindPropertyRelative("label");
            do {
                EditorGUI.PropertyField(drawPos, prop, true);
                drawPos.x += drawPos.width + 8;
            }
            while (prop.NextVisible(false) && prop.type != "FgdFlag" && prop.type != "FgdEnum");
  
            EditorGUI.EndProperty();
            // EditorUtility.SetDirty(property.serializedObject.targetObject);
        }
    }

    [CustomPropertyDrawer(typeof(ScopaFgdConfig.FgdFlag))]
    public class ScopaFgdFlagDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return base.GetPropertyHeight(property, label);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var drawPos = new Rect(position);
            drawPos.width /= 2;

            SerializedProperty prop = property.FindPropertyRelative("defaultValue");
            do {
                if ( prop.name == "defaultValue") {
                    EditorGUI.PropertyField(new Rect(drawPos.x, drawPos.y, 24, drawPos.height), prop, new GUIContent("", prop.tooltip));
                    drawPos.x += 24;
                } else if (prop.name == "bitValue") {
                    EditorGUI.PropertyField(new Rect(drawPos.x, drawPos.y, drawPos.width - 24, drawPos.height), prop, new GUIContent(" ", prop.tooltip));
                } else {
                    EditorGUI.PropertyField(drawPos, prop, true);
                    drawPos.x += drawPos.width;
                }
            }
            while (prop.NextVisible(false) && prop.type != "FgdFlag" && prop.type != "FgdEnum" && prop.type != "FgdProperty" && prop.type != "FgdClassBase" && prop.type != "ScopaFgdConfigAsset"  && prop.type != "ScopaFgdConfig" && prop.name != "includeFgds");

  
            EditorGUI.EndProperty();
            // EditorUtility.SetDirty(property.serializedObject.targetObject);
        }
    }
}