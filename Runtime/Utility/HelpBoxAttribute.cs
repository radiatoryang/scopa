using UnityEngine;

namespace Scopa {
    public enum HelpBoxMessageType { None, Info, Warning, Error }

    // from https://discussions.unity.com/t/helpattribute-allows-you-to-use-helpbox-in-the-unity-inspector-window/659414/4
    public class HelpBoxAttribute : PropertyAttribute {

        public string text;
        public HelpBoxMessageType messageType;

        public HelpBoxAttribute(string text, HelpBoxMessageType messageType = HelpBoxMessageType.None) {
            this.text = text;
            this.messageType = messageType;
        }
    }
}