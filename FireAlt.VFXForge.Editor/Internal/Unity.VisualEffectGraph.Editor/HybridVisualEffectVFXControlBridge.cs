using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.VFX;

namespace UnityEditor.VFX.UI
{
    [InitializeOnLoad]
    internal static class HybridVisualEffectVFXControlBridge
    {
        private const string HybridVisualEffectTypeName = "KrasCore.HybridECS.HybridVisualEffect, KrasCore.HybridECS";
        private static readonly object PatchMarker = new();
        private static readonly FieldInfo DebugUIField = typeof(VFXComponentBoard).GetField("m_DebugUI", BindingFlags.Instance | BindingFlags.NonPublic);

        private static Type s_HybridVisualEffectType;
        private static MethodInfo s_EditorControlStopMethod;
        private static MethodInfo s_EditorControlPlayPauseMethod;
        private static MethodInfo s_EditorControlStepMethod;
        private static MethodInfo s_EditorControlRestartMethod;
        private static MethodInfo s_EditorPlayMethod;
        private static MethodInfo s_EditorStopMethod;

        static HybridVisualEffectVFXControlBridge()
        {
            EditorApplication.update += Update;
            Selection.selectionChanged += Update;
            EditorApplication.delayCall += Update;
        }

        private static void Update()
        {
            AttachSelectedHybridToMatchingWindows();

            foreach (var window in VFXViewWindow.GetAllWindows())
            {
                PatchWindow(window);
            }
        }

        private static void PatchWindow(VFXViewWindow window)
        {
            var graphView = window?.graphView;
            if (graphView == null)
            {
                return;
            }

            var board = graphView.Q<VFXComponentBoard>();
            if (board == null)
            {
                return;
            }

            var stopButton = board.Q<Button>("stop");
            if (stopButton == null || ReferenceEquals(stopButton.userData, PatchMarker))
            {
                return;
            }

            PatchButton(stopButton, () => OnStop(board));
            PatchButton(board.Q<Button>("play"), () => OnPlayPause(board));
            PatchButton(board.Q<Button>("step"), () => OnStep(board));
            PatchButton(board.Q<Button>("restart"), () => OnRestart(board));
            PatchButton(board.Q<Button>("on-play-button"), () => OnPlayEvent(board));
            PatchButton(board.Q<Button>("on-stop-button"), () => OnStopEvent(board));
        }

        private static void PatchButton(Button button, Action handler)
        {
            if (button == null)
            {
                return;
            }

            button.clickable = new Clickable(handler);
            button.userData = PatchMarker;
        }

        private static void AttachSelectedHybridToMatchingWindows()
        {
            var visualEffect = GetSelectedHybridVisualEffect();
            if (visualEffect == null || visualEffect.visualEffectAsset == null)
            {
                return;
            }

            foreach (var window in VFXViewWindow.GetAllWindows())
            {
                var graphView = window?.graphView;
                if (graphView?.controller?.graph?.visualEffectResource?.asset != visualEffect.visualEffectAsset)
                {
                    continue;
                }

                if (graphView.attachedComponent == visualEffect)
                {
                    continue;
                }

                graphView.attachedComponent = visualEffect;
                window.Repaint();
            }
        }

        private static VisualEffect GetSelectedHybridVisualEffect()
        {
            var selectedGameObject = Selection.activeGameObject;
            if (selectedGameObject == null)
            {
                return null;
            }

            return GetHybridComponent(selectedGameObject.GetComponent<VisualEffect>()) != null
                ? selectedGameObject.GetComponent<VisualEffect>()
                : null;
        }

        private static void OnStop(VFXComponentBoard board)
        {
            var effect = board.GetAttachedComponent();
            if (effect == null)
            {
                return;
            }

            if (!InvokeHybrid(effect, ref s_EditorControlStopMethod, "EditorControlStop"))
            {
                effect.ControlStop();
            }

            NotifyDebug(board, VFXUIDebug.Events.VFXStop);
            RepaintViews();
        }

        private static void OnPlayPause(VFXComponentBoard board)
        {
            var effect = board.GetAttachedComponent();
            if (effect == null)
            {
                return;
            }

            if (!InvokeHybrid(effect, ref s_EditorControlPlayPauseMethod, "EditorControlPlayPause"))
            {
                effect.ControlPlayPause();
            }

            NotifyDebug(board, VFXUIDebug.Events.VFXPlayPause);
            RepaintViews();
        }

        private static void OnStep(VFXComponentBoard board)
        {
            var effect = board.GetAttachedComponent();
            if (effect == null)
            {
                return;
            }

            if (!InvokeHybrid(effect, ref s_EditorControlStepMethod, "EditorControlStep"))
            {
                effect.ControlStep();
            }

            NotifyDebug(board, VFXUIDebug.Events.VFXStep);
            RepaintViews();
        }

        private static void OnRestart(VFXComponentBoard board)
        {
            var effect = board.GetAttachedComponent();
            if (effect == null)
            {
                return;
            }

            if (!InvokeHybrid(effect, ref s_EditorControlRestartMethod, "EditorControlRestart"))
            {
                effect.ControlRestart();
            }

            NotifyDebug(board, VFXUIDebug.Events.VFXReset);
            RepaintViews();
        }

        private static void OnPlayEvent(VFXComponentBoard board)
        {
            var effect = board.GetAttachedComponent();
            if (effect == null)
            {
                return;
            }

            if (!InvokeHybrid(effect, ref s_EditorPlayMethod, "EditorPlay"))
            {
                board.SendEvent(VisualEffectAsset.PlayEventName);
            }

            RepaintViews();
        }

        private static void OnStopEvent(VFXComponentBoard board)
        {
            var effect = board.GetAttachedComponent();
            if (effect == null)
            {
                return;
            }

            if (!InvokeHybrid(effect, ref s_EditorStopMethod, "EditorStop"))
            {
                board.SendEvent(VisualEffectAsset.StopEventName);
            }

            RepaintViews();
        }

        private static bool InvokeHybrid(VisualEffect effect, ref MethodInfo method, string methodName)
        {
            var hybridComponent = GetHybridComponent(effect);
            if (hybridComponent == null)
            {
                return false;
            }

            method ??= GetHybridMethod(methodName);
            if (method == null)
            {
                return false;
            }

            method.Invoke(hybridComponent, null);
            return true;
        }

        private static object GetHybridComponent(VisualEffect effect)
        {
            if (effect == null)
            {
                return null;
            }

            var hybridType = GetHybridVisualEffectType();
            return hybridType != null ? effect.GetComponent(hybridType) : null;
        }

        private static Type GetHybridVisualEffectType()
        {
            if (s_HybridVisualEffectType == null)
            {
                s_HybridVisualEffectType = Type.GetType(HybridVisualEffectTypeName);
                if (s_HybridVisualEffectType == null)
                {
                    foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        s_HybridVisualEffectType = assembly.GetType("KrasCore.HybridECS.HybridVisualEffect");
                        if (s_HybridVisualEffectType != null)
                        {
                            break;
                        }
                    }
                }
            }

            return s_HybridVisualEffectType;
        }

        private static MethodInfo GetHybridMethod(string methodName)
        {
            var hybridType = GetHybridVisualEffectType();
            return hybridType?.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        }

        private static void NotifyDebug(VFXComponentBoard board, VFXUIDebug.Events debugEvent)
        {
            if (DebugUIField?.GetValue(board) is VFXUIDebug debugUI)
            {
                debugUI.Notify(debugEvent);
            }
        }

        private static void RepaintViews()
        {
            EditorApplication.QueuePlayerLoopUpdate();
            SceneView.RepaintAll();

            foreach (var window in VFXViewWindow.GetAllWindows())
            {
                window.Repaint();
            }
        }
    }
}
