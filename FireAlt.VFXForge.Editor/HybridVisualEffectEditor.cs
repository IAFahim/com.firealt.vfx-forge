using System.Collections.Generic;
using ArtificeToolkit.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace FireAlt.VFXForge.Editor
{
    [CustomEditor(typeof(HybridVisualEffect))]
    public class HybridVisualEffectEditor : UnityEditor.Editor
    {
        private const string UnityVfxOverlayId = "Scene View/Visual Effect";

        private static HybridVisualEffect s_ActiveEffect;
        private static readonly Dictionary<SceneView, bool> HiddenUnityVfxOverlayStates = new();
        private HybridVisualEffect _targetEffect;

        internal static HybridVisualEffect ActiveEffect => s_ActiveEffect;
        
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            root.Add(new ArtificeDrawer().CreateInspectorGUI(serializedObject));
            return root;
        }

        private void OnEnable()
        {
            _targetEffect = (HybridVisualEffect)target;
            s_ActiveEffect = _targetEffect;
            _targetEffect.OnInspectorOpened();

            SceneView.duringSceneGui += HideUnityVfxOverlay;
            foreach (var sceneView in SceneView.sceneViews)
            {
                HideUnityVfxOverlay((SceneView)sceneView);
            }
        }

        private void OnDisable()
        {
            if (_targetEffect != null)
            {
                _targetEffect.OnInspectorClosed();
            }

            if (s_ActiveEffect == _targetEffect)
            {
                s_ActiveEffect = null;
            }

            SceneView.duringSceneGui -= HideUnityVfxOverlay;
            RestoreUnityVfxOverlayInOpenViews();
        }

        private static void HideUnityVfxOverlay(SceneView sceneView)
        {
            if (sceneView == null || !sceneView.TryGetOverlay(UnityVfxOverlayId, out var overlay))
            {
                return;
            }

            if (!HiddenUnityVfxOverlayStates.ContainsKey(sceneView))
            {
                HiddenUnityVfxOverlayStates.Add(sceneView, overlay.displayed);
            }

            if (overlay.displayed)
            {
                overlay.displayed = false;
            }
        }

        private static void RestoreUnityVfxOverlayInOpenViews()
        {
            foreach (var pair in HiddenUnityVfxOverlayStates)
            {
                if (pair.Key != null && pair.Key.TryGetOverlay(UnityVfxOverlayId, out var overlay) && overlay != null)
                {
                    overlay.displayed = pair.Value;
                }
            }

            HiddenUnityVfxOverlayStates.Clear();
        }
    }
}
