using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace FireAlt.VFXForge.Editor
{
    [CustomEditor(typeof(HybridVisualEffect))]
    public class HybridVisualEffectEditor : UnityEditor.Editor
    {
        private const float BOX_BORDER_WIDTH = 1f;
        private const float BOX_ACCENT_BORDER_WIDTH = 3f;
        private const int VISIBILITY_REFRESH_INTERVAL_MS = 200;
        private const string UNITY_VFX_OVERLAY_ID = "Scene View/Visual Effect";
        private static readonly Color ACCENT_ORANGE = new(0.702f, 0.420f, 0.129f, 1f); // #b36b21
        private static readonly Color ACCENT_ORANGE_TEXT = new(0.941f, 0.702f, 0.431f, 1f); // #f0b36e
        private static readonly Color HEADER_BG = new(0.165f, 0.165f, 0.165f, 1f);
        private static readonly Color BODY_BG = new(0.247f, 0.247f, 0.247f, 1f);

        private static HybridVisualEffect s_ActiveEffect;
        private static readonly Dictionary<SceneView, bool> HiddenUnityVfxOverlayStates = new();
        private HybridVisualEffect _targetEffect;

        internal static HybridVisualEffect ActiveEffect => s_ActiveEffect;
        
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            var conditionBindings = new List<ConditionBinding>();

            var vfxDefinitionField = CreatePropertyField("_vfxDefinition");
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

        private PropertyField CreatePropertyField(string propertyName)
        {
            var property = serializedObject.FindProperty(propertyName);
            return property == null ? null : new PropertyField(property);
        }

        private static VisualElement CreateBoxGroup(string title, VisualElement content)
        {
            var group = new VisualElement();
            group.style.marginTop = 4f;
            group.style.marginBottom = 4f;

            var header = new Label(title);
            header.style.color = ACCENT_ORANGE_TEXT;
            header.style.backgroundColor = HEADER_BG;
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.borderTopWidth = BOX_BORDER_WIDTH;
            header.style.borderBottomWidth = BOX_BORDER_WIDTH;
            header.style.borderLeftWidth = BOX_ACCENT_BORDER_WIDTH;
            header.style.borderRightWidth = BOX_BORDER_WIDTH;
            header.style.borderTopColor = Color.black;
            header.style.borderBottomColor = Color.black;
            header.style.borderLeftColor = ACCENT_ORANGE;
            header.style.borderRightColor = Color.black;
            header.style.paddingTop = 4f;
            header.style.paddingBottom = 4f;
            header.style.paddingLeft = 6f;
            header.style.paddingRight = 6f;

            var body = new VisualElement();
            body.style.backgroundColor = BODY_BG;
            body.style.borderLeftWidth = BOX_BORDER_WIDTH;
            body.style.borderRightWidth = BOX_BORDER_WIDTH;
            body.style.borderBottomWidth = BOX_BORDER_WIDTH;
            body.style.borderLeftColor = Color.black;
            body.style.borderRightColor = Color.black;
            body.style.borderBottomColor = Color.black;
            body.style.paddingTop = 4f;
            body.style.paddingBottom = 6f;
            body.style.paddingLeft = 6f;
            body.style.paddingRight = 6f;

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
