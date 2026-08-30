// StageEventWiringHelper.cs
// Wires the toolkit's own event components (which DO resolve in-game) to simple
// target actions, and sets the private combo/score/time target via SerializedObject.
// Supported actions: GameObject.SetActive(bool), Behaviour.enabled(bool).
#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;

namespace CustomStage.Stagecraft
{
    public class StageEventWiringHelper : EditorWindow
    {
        private enum Source { StageEvents, ComboEvents, ScoreEvents, TimeEvents, SpecialsFX }
        private enum Action { SetActive, EnableComponent }

        private Source source = Source.StageEvents;
        private Component sourceComp;
        private string eventField = "OnSongStart";
        private int comboScoreTarget = 50;
        private float timeTarget = 30f;

        private Action action = Action.SetActive;
        private GameObject targetGO;
        private Behaviour targetBehaviour;
        private bool boolValue = true;

        [MenuItem("SynthRiders/Stagecraft/8. Stage Event Wiring")]
        public static void ShowWindow() => GetWindow<StageEventWiringHelper>("Event Wiring").minSize = new Vector2(380, 360);

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Source", EditorStyles.boldLabel);
            source = (Source)EditorGUILayout.EnumPopup("Component Type", source);
            sourceComp = (Component)EditorGUILayout.ObjectField("Source Component", sourceComp, typeof(Component), true);
            eventField = EditorGUILayout.TextField("UnityEvent field", eventField);
            EditorGUILayout.HelpBox(EventHint(), MessageType.None);

            if (source == Source.ComboEvents || source == Source.ScoreEvents)
                comboScoreTarget = EditorGUILayout.IntField("Target (combo/score)", comboScoreTarget);
            if (source == Source.TimeEvents)
                timeTarget = EditorGUILayout.FloatField("Target (seconds)", timeTarget);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Action", EditorStyles.boldLabel);
            action = (Action)EditorGUILayout.EnumPopup("Action", action);
            if (action == Action.SetActive)
                targetGO = (GameObject)EditorGUILayout.ObjectField("Target GameObject", targetGO, typeof(GameObject), true);
            else
                targetBehaviour = (Behaviour)EditorGUILayout.ObjectField("Target Behaviour", targetBehaviour, typeof(Behaviour), true);
            boolValue = EditorGUILayout.Toggle("Value (on/off)", boolValue);

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(!Ready()))
                if (GUILayout.Button("Wire It")) Wire();
        }

        private string EventHint() => source switch
        {
            Source.StageEvents => "OnSongStart, OnSongEnd, OnNoteHit, OnNoteFail, OnEnterSpecial, OnCompleteSpecial, OnFailSpecial",
            Source.ComboEvents => "OnNthCombo",
            Source.ScoreEvents => "OnScore",
            Source.TimeEvents  => "OnTimeTick",
            Source.SpecialsFX  => "SpecialStartFX, SpecialEndFX, SpecialFailFX",
            _ => ""
        };

        private bool Ready()
        {
            if (sourceComp == null || string.IsNullOrEmpty(eventField)) return false;
            return action == Action.SetActive ? targetGO != null : targetBehaviour != null;
        }

        private void Wire()
        {
            // Set the private numeric target where relevant.
            if (source == Source.ComboEvents || source == Source.ScoreEvents)
                SetSerialized("target", comboScoreTarget, null);
            if (source == Source.TimeEvents)
                SetSerialized("target", null, timeTarget);

            var evt = GetUnityEvent(sourceComp, eventField);
            if (evt == null) { EditorUtility.DisplayDialog("Wiring", $"No UnityEvent field '{eventField}' on {sourceComp.GetType().Name}.", "OK"); return; }

            Undo.RecordObject(sourceComp, "Wire Event");
            if (action == Action.SetActive)
            {
                UnityAction<bool> call = targetGO.SetActive;
                UnityEventTools.AddBoolPersistentListener(evt, call, boolValue);
            }
            else
            {
                // Bind a persistent listener to the Behaviour's set_enabled(bool),
                // so the call survives serialization (no lambda, which wouldn't).
                var setEnabled = typeof(Behaviour).GetProperty("enabled").GetSetMethod();
                var call = (UnityAction<bool>)Delegate.CreateDelegate(
                    typeof(UnityAction<bool>), targetBehaviour, setEnabled);
                UnityEventTools.AddBoolPersistentListener(evt, call, boolValue);
            }
            EditorUtility.SetDirty(sourceComp);
            StagecraftUtil.MarkSceneDirty();
        }

        private void SetSerialized(string prop, int? iv, float? fv)
        {
            var so = new SerializedObject(sourceComp);
            var p = so.FindProperty(prop);
            if (p == null) return;
            if (iv.HasValue) p.intValue = iv.Value;
            if (fv.HasValue) p.floatValue = fv.Value;
            so.ApplyModifiedProperties();
        }

        private static UnityEvent GetUnityEvent(Component c, string field)
        {
            var f = c.GetType().GetField(field, BindingFlags.Public | BindingFlags.Instance);
            return f?.GetValue(c) as UnityEvent;
        }
    }
}
#endif
