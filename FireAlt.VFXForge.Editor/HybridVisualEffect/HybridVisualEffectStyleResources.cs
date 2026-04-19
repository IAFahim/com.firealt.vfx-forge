using KrasCore.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace FireAlt.VFXForge.Editor
{
    [InitializeOnLoad]
    public static class HybridVisualEffectStyleResources
    {
        public static readonly StyleSheet HybridVisualEffectEditorStyleSheet;

        private const string ValidationRoot = "FireAlt.VFXForge.Editor";

        static HybridVisualEffectStyleResources()
        {
            HybridVisualEffectEditorStyleSheet = Load<StyleSheet>("Styles/HybridVisualEffectEditor.uss");
        }

        private static T Load<T>(string path) where T : Object
        {
            return AssetDatabaseUtils.LoadEditorResource<T>(path, ValidationRoot);
        }
    }
}
