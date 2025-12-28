using UnityEngine;
using UnityEditor;

namespace Scopa.Editor {

    // from https://discussions.unity.com/t/helpattribute-allows-you-to-use-helpbox-in-the-unity-inspector-window/659414/4
    [CustomPropertyDrawer(typeof(HelpBoxAttribute))]
    public class HelpBoxAttributeDrawer : DecoratorDrawer {

        public override float GetHeight() {
            try {
                var helpBoxAttribute = attribute as HelpBoxAttribute;
                if (helpBoxAttribute == null) return base.GetHeight();
                var helpBoxStyle = (GUI.skin != null) ? GUI.skin.GetStyle("helpbox") : null;
                if (helpBoxStyle == null) return base.GetHeight();
                return Mathf.Max(40f, helpBoxStyle.CalcHeight(new GUIContent(helpBoxAttribute.text), EditorGUIUtility.currentViewWidth) + 4);
            }
            catch (System.ArgumentException) {
                return 3 * EditorGUIUtility.singleLineHeight; // Handle Unity 2022.2 bug by returning default value.
            }
        }

        public override void OnGUI(Rect position) {
            var helpBoxAttribute = attribute as HelpBoxAttribute;
            if (helpBoxAttribute == null) return;
            EditorGUI.HelpBox(position, helpBoxAttribute.text, GetMessageType(helpBoxAttribute.messageType));
        }

        private MessageType GetMessageType(HelpBoxMessageType helpBoxMessageType) {
            switch (helpBoxMessageType) {
                default:
                case HelpBoxMessageType.None: return MessageType.None;
                case HelpBoxMessageType.Info: return MessageType.Info;
            case HelpBoxMessageType.Warning: return MessageType.Warning;
                case HelpBoxMessageType.Error: return MessageType.Error;
            }
        }
    }
}