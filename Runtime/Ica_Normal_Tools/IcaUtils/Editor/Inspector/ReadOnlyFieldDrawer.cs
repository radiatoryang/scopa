using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Ica.Utils.Editor
{
    [CustomPropertyDrawer(typeof(ReadOnlyFieldAttribute))]
    public class ReadOnlyFieldDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginDisabledGroup(true); // Disable the field
            EditorGUI.PropertyField(position, property, label);
            EditorGUI.EndDisabledGroup();
        }
    }
}