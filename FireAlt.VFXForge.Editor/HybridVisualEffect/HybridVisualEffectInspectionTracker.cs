using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FireAlt.VFXForge.Editor
{
    [InitializeOnLoad]
    internal static class HybridVisualEffectInspectionTracker
    {
        private const string UNITY_VFX_OVERLAY_ID = "Scene View/Visual Effect";

        private static readonly Dictionary<SceneView, bool> HiddenUnityVfxOverlayStates = new();
        private static readonly List<HybridVisualEffectEditor> RegisteredEditors = new();

        private static List<HybridVisualEffect> _activeEffects = new();
        private static bool _isRebuildQueued;
        private static bool _isUnityVfxOverlayHidden;

        static HybridVisualEffectInspectionTracker()
        {
            Selection.selectionChanged += RequestRebuild;
            AssemblyReloadEvents.beforeAssemblyReload += RestoreUnityVfxOverlayState;
            EditorApplication.quitting += RestoreUnityVfxOverlayState;
            EditorApplication.delayCall += RequestRebuild;
        }

        internal static HybridVisualEffect PrimaryEffect => _activeEffects.Count > 0 ? _activeEffects[0] : null;

        internal static void RegisterEditor(HybridVisualEffectEditor editor)
        {
            if (editor == null || RegisteredEditors.Contains(editor))
            {
                return;
            }

            RegisteredEditors.Add(editor);
            RequestRebuild();
        }

        internal static void UnregisterEditor(HybridVisualEffectEditor editor)
        {
            if (editor == null)
            {
                return;
            }

            if (RegisteredEditors.Remove(editor))
            {
                RequestRebuild();
            }
        }

        internal static void RequestRebuild()
        {
            if (_isRebuildQueued)
            {
                return;
            }

            _isRebuildQueued = true;
            EditorApplication.delayCall += RebuildIfQueued;
        }

        private static void RebuildIfQueued()
        {
            if (!_isRebuildQueued)
            {
                return;
            }

            _isRebuildQueued = false;
            RebuildNow();
        }

        private static void RebuildNow()
        {
            RemoveDeadEditors();

            var nextEffects = new List<HybridVisualEffect>();
            var seenEffects = new HashSet<HybridVisualEffect>();

            // Selection drives the primary target, while registered editor instances keep locked inspectors alive.
            AppendSelectedEffects(nextEffects, seenEffects);

            for (var i = 0; i < RegisteredEditors.Count; i++)
            {
                RegisteredEditors[i].AppendInspectedEffects(nextEffects, seenEffects);
            }

            if (_activeEffects.Count == nextEffects.Count && _activeEffects.SequenceEqual(nextEffects))
            {
                UpdateUnityVfxOverlayVisibility(_activeEffects.Count > 0);
                return;
            }

            var previousEffects = new HashSet<HybridVisualEffect>(_activeEffects);
            var nextEffectSet = new HashSet<HybridVisualEffect>(nextEffects);

            for (var i = 0; i < _activeEffects.Count; i++)
            {
                var effect = _activeEffects[i];
                if (effect == null || !nextEffectSet.Contains(effect))
                {
                    effect?.OnInspectorClosed();
                }
            }

            for (var i = 0; i < nextEffects.Count; i++)
            {
                var effect = nextEffects[i];
                if (!previousEffects.Contains(effect))
                {
                    effect.OnInspectorOpened();
                }
            }

            _activeEffects = nextEffects;
            UpdateUnityVfxOverlayVisibility(_activeEffects.Count > 0);
        }

        private static void AppendSelectedEffects(List<HybridVisualEffect> effects, HashSet<HybridVisualEffect> seenEffects)
        {
            var selectedGameObjects = Selection.gameObjects;
            for (var i = 0; i < selectedGameObjects.Length; i++)
            {
                var gameObject = selectedGameObjects[i];
                if (gameObject != null
                    && gameObject.TryGetComponent(out HybridVisualEffect effect)
                    && effect != null
                    && seenEffects.Add(effect))
                {
                    effects.Add(effect);
                }
            }
        }

        private static void RemoveDeadEditors()
        {
            for (var i = RegisteredEditors.Count - 1; i >= 0; i--)
            {
                if (RegisteredEditors[i] == null)
                {
                    RegisteredEditors.RemoveAt(i);
                }
            }
        }

        private static void UpdateUnityVfxOverlayVisibility(bool shouldHide)
        {
            if (shouldHide)
            {
                if (!_isUnityVfxOverlayHidden)
                {
                    _isUnityVfxOverlayHidden = true;
                    SceneView.duringSceneGui += HideUnityVfxOverlay;
                }

                foreach (var sceneView in SceneView.sceneViews.OfType<SceneView>())
                {
                    HideUnityVfxOverlay(sceneView);
                }

                return;
            }

            RestoreUnityVfxOverlayState();
        }

        private static void HideUnityVfxOverlay(SceneView sceneView)
        {
            if (sceneView == null || !sceneView.TryGetOverlay(UNITY_VFX_OVERLAY_ID, out var overlay))
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
                if (pair.Key != null && pair.Key.TryGetOverlay(UNITY_VFX_OVERLAY_ID, out var overlay) && overlay != null)
                {
                    overlay.displayed = pair.Value;
                }
            }

            HiddenUnityVfxOverlayStates.Clear();
        }

        private static void RestoreUnityVfxOverlayState()
        {
            if (_isUnityVfxOverlayHidden)
            {
                SceneView.duringSceneGui -= HideUnityVfxOverlay;
                _isUnityVfxOverlayHidden = false;
            }

            RestoreUnityVfxOverlayInOpenViews();
        }
    }
}
