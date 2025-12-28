using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

namespace Scopa.Editor {
    public class ScopaProjectSettingsProvider : SettingsProvider
    {
        private ScopaProjectSettings settings;
        private ScopaProjectSettingsEditor editor;

        public ScopaProjectSettingsProvider(string path, SettingsScope scope = SettingsScope.Project)
            : base(path, scope) {}

        public override void OnActivate(string searchContext, VisualElement rootElement) {
            settings = ScopaProjectSettings.Get();
            editor = UnityEditor.Editor.CreateEditor(settings) as ScopaProjectSettingsEditor;
            editor.showWarning = false;
            base.OnActivate(searchContext, rootElement);
        }

        public override void OnDeactivate() {
            settings = null;
            UnityEngine.Object.DestroyImmediate(editor);
            base.OnDeactivate();
        }

        public override void OnGUI(string searchContext) {
            EditorGUI.indentLevel++;
            EditorGUI.BeginChangeCheck();
            editor.OnInspectorGUI();
            EditorGUI.indentLevel--;

            if (EditorGUI.EndChangeCheck()) {
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets();
            }
        }

        [SettingsProvider]
        static SettingsProvider CreateProvider() {
            var provider = new ScopaProjectSettingsProvider("Project/Scopa", SettingsScope.Project);
            provider.settings = ScopaProjectSettings.Get();
            provider.keywords = GetSearchKeywordsFromSerializedObject(new SerializedObject(provider.settings));
            return provider;
        }

    }
}