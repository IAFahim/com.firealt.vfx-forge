using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using KrasCore.Editor;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace FireAlt.VFXForge.Editor
{
    [CustomEditor(typeof(HybridVisualEffect))]
    public class HybridVisualEffectEditor : UnityEditor.Editor
    {
        private const int VISIBILITY_REFRESH_INTERVAL_MS = 200;
        private const string UNITY_VFX_OVERLAY_ID = "Scene View/Visual Effect";
        private const string VFX_DEFINITION_PROPERTY_NAME = "_vfxDefinition";

        private static HybridVisualEffect s_ActiveEffect;
        private static readonly Dictionary<SceneView, bool> HiddenUnityVfxOverlayStates = new();
        private HybridVisualEffect _targetEffect;

        internal static HybridVisualEffect ActiveEffect => s_ActiveEffect;
        
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            root.styleSheets.Add(DrawerStyleResources.CommonStyleSheet);
            root.styleSheets.Add(HybridVisualEffectStyleResources.HybridVisualEffectEditorStyleSheet);

            var conditionBindings = new List<ConditionBinding>();
            var vfxDefinitionProperty = serializedObject.FindProperty(VFX_DEFINITION_PROPERTY_NAME);

            var vfxDefinitionField = CreatePropertyField(VFX_DEFINITION_PROPERTY_NAME);
            if (vfxDefinitionField != null)
            {
                root.Add(vfxDefinitionField);
            }

            var uploadDataField = CreatePropertyField("_uploadData");
            if (uploadDataField != null)
            {
                var uploadDataBox = CreateBoxGroup("VFX Data Type Baker", uploadDataField);
                root.Add(uploadDataBox);
                conditionBindings.Add(new ConditionBinding(uploadDataBox, "IsDefinitionValid", true));
            }

            var uploadArrayDataField = CreatePropertyField("_uploadArrayData");
            if (uploadArrayDataField != null)
            {
                var uploadArrayDataBox = CreateBoxGroup("VFX Array Data Type Baker", uploadArrayDataField);
                root.Add(uploadArrayDataBox);
                conditionBindings.Add(new ConditionBinding(uploadArrayDataBox, "IsDefinitionValid", true));
            }

            var trackingDurationField = CreatePropertyField("_trackingDuration");
            if (trackingDurationField != null)
            {
                root.Add(trackingDurationField);
                conditionBindings.Add(new ConditionBinding(trackingDurationField, "ShowTrackingDuration", true));
            }

            var focusedBoundsField = CreatePropertyField("focusedBoundsSize");
            if (focusedBoundsField != null)
            {
                root.Add(focusedBoundsField);
            }

            if (vfxDefinitionProperty != null)
            {
                root.TrackPropertyValue(vfxDefinitionProperty, _ => RefreshDataAndReinitTargets());
            }

            void RefreshVisibility()
            {
                for (var i = 0; i < conditionBindings.Count; i++)
                {
                    var binding = conditionBindings[i];
                    binding.Target.style.display = EvaluateMethodCondition(binding.MethodName, binding.Values)
                        ? DisplayStyle.Flex
                        : DisplayStyle.None;
                }
            }

            root.TrackSerializedObjectValue(serializedObject, _ => RefreshVisibility());
            root.schedule.Execute(RefreshVisibility).Every(VISIBILITY_REFRESH_INTERVAL_MS);
            RefreshVisibility();
            return root;
        }

        private void RefreshDataAndReinitTargets()
        {
            var targets = serializedObject.targetObjects;
            if (targets == null || targets.Length == 0)
            {
                return;
            }

            for (var i = 0; i < targets.Length; i++)
            {
                if (targets[i] is HybridVisualEffect effect)
                {
                    effect.RefreshDataAndReinit();
                }
            }
        }

        private PropertyField CreatePropertyField(string propertyName)
        {
            var property = serializedObject.FindProperty(propertyName);
            return property == null ? null : new PropertyField(property);
        }

        private static VisualElement CreateBoxGroup(string title, VisualElement content)
        {
            var group = new VisualElement();
            group.AddToClassList("hybrid-vfx-editor-box-group");

            var header = new Label(title);
            header.AddToClassList("kras-drawer-box-header");
            header.AddToClassList("kras-drawer-box-title");

            var body = new VisualElement();
            body.AddToClassList("kras-drawer-box-body");
            body.Add(content);

            group.Add(header);
            group.Add(body);
            return group;
        }

        private bool EvaluateMethodCondition(string methodName, IReadOnlyList<object> expectedValues)
        {
            var targets = serializedObject.targetObjects;
            if (targets == null || targets.Length == 0)
            {
                return true;
            }

            for (var i = 0; i < targets.Length; i++)
            {
                if (TryInvokeConditionMethod(targets[i], methodName, out var conditionValue) &&
                    MatchesAnyExpectedValue(conditionValue, expectedValues))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryInvokeConditionMethod(object target, string methodName, out object value)
        {
            value = null;
            if (target == null || string.IsNullOrWhiteSpace(methodName))
            {
                return false;
            }

            var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (method == null || method.GetParameters().Length != 0)
            {
                return false;
            }

            value = method.Invoke(target, null);
            return true;
        }

        private static bool MatchesAnyExpectedValue(object actualValue, IReadOnlyList<object> expectedValues)
        {
            if (expectedValues == null || expectedValues.Count == 0)
            {
                return AreEqual(actualValue, true);
            }

            for (var i = 0; i < expectedValues.Count; i++)
            {
                if (AreEqual(actualValue, expectedValues[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool AreEqual(object left, object right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            if (left == null || right == null)
            {
                return false;
            }

            var leftType = left.GetType();
            var rightType = right.GetType();
            if ((leftType.IsEnum || rightType.IsEnum) &&
                TryConvertToInt64(left, out var leftInt) &&
                TryConvertToInt64(right, out var rightInt))
            {
                return leftInt == rightInt;
            }

            return left.Equals(right);
        }

        private static bool TryConvertToInt64(object value, out long result)
        {
            try
            {
                result = Convert.ToInt64(value, CultureInfo.InvariantCulture);
                return true;
            }
            catch
            {
                result = 0;
                return false;
            }
        }

        private readonly struct ConditionBinding
        {
            public ConditionBinding(VisualElement target, string methodName, params object[] values)
            {
                Target = target;
                MethodName = methodName;
                Values = values ?? Array.Empty<object>();
            }

            public VisualElement Target { get; }
            public string MethodName { get; }
            public IReadOnlyList<object> Values { get; }
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
    }
}
