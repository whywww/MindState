using System;
using UnityEngine;

namespace MindState.BCI.BrainLink
{
    [DefaultExecutionOrder(10), DisallowMultipleComponent]
    public sealed class BrainLinkFlowerInput : MonoBehaviour
    {
        public FlowerAssembler flower;
        public BrainLinkSettings settings = new BrainLinkSettings();
        public BrainLinkClient Client { get; private set; }
        public bool ShowDiagnostics { get; private set; }
        public bool ManualWindowActive => Time.unscaledTime < manualUntil;
        public float ManualWindowRemaining => Mathf.Max(0, manualUntil - Time.unscaledTime);
        long appliedRevision;
        float manualUntil;
        bool wasManual;
        float nextLog;
        string previousState, error;

        public void NoteManualInteraction() => manualUntil = Time.unscaledTime + 5;

        void OnEnable()
        {
            if (!Application.isPlaying) return;
            appliedRevision = 0; manualUntil = 0; wasManual = false; nextLog = 0;
            previousState = error = null;
            try
            {
                Client = new BrainLinkClient(new BrainLinkSettings { host=settings.host, port=settings.port });
                Client.Start();
            }
            catch (Exception exception)
            { error = exception.Message; Debug.LogError("[BrainLink] " + error, this); }
        }

        void Update()
        {
            var sample = ReadSnapshot();
            bool manual = ManualWindowActive;
            // Without usable data the flower stays on the gallery's manual loop; never force a waiting state.
            if (flower != null && flower.Ready && !manual && sample.Usable
                && (sample.Revision != appliedRevision || wasManual))
            {
                if (flower.ArtInput != FlowerArtInput.External) flower.WaitForArtBrain();
                flower.SetBrainMetrics(sample.Attention / 100f, sample.Meditation / 100f);
                appliedRevision = sample.Revision;
            }
            wasManual = manual;
            string state = sample.State + ":" + sample.Mode + ":" + sample.Signal;
            if (Time.unscaledTime >= nextLog || state != previousState)
            {
                Debug.Log("[BrainLink] " + sample.Describe(), this);
                previousState = state; nextLog = Time.unscaledTime + 1;
            }
        }

        public void ToggleDiagnostics() => ShowDiagnostics = !ShowDiagnostics;
        public BrainLinkSnapshot ReadSnapshot() => Client != null ? Client.Snapshot()
            : new BrainLinkSnapshot { Status=error ?? (isActiveAndEnabled ? "Starting" : "Disabled in Inspector"),
                Attention=-1, Meditation=-1, Signal=-1, Age=double.PositiveInfinity };

        static string Percent(Vector4 w) => Mathf.RoundToInt(w.x * 100) + "/" + Mathf.RoundToInt(w.y * 100) + "/" + Mathf.RoundToInt(w.z * 100) + "/" + Mathf.RoundToInt(w.w * 100);
        // Drawn by FlowerGallery so F9 can also report a disabled component.
        public void DrawDiagnostics(GUIStyle style)
        {
            if (!ShowDiagnostics) return;
            var sample = ReadSnapshot();
            GUI.Box(new Rect(1320, 30, 570, 270), GUIContent.none);
            string values = sample.HasSample
                ? $"专注度 attention: {sample.Attention} / 100\n放松度 meditation: {sample.Meditation} / 100\n佩戴信号 signal: {sample.Signal}（0 为良好）\n距最近数据: {sample.Age:F1}s" + (sample.Fresh ? "" : " · 已过期")
                : "暂无数据";
            string mapping = flower != null
                ? "\n权重 原始 " + Percent(flower.RawBrainWeights) + " / 显示 " + Percent(flower.DisplayWeights)
                  + " · 基线 " + Mathf.RoundToInt(flower.BaselineAttention * 100) + "/" + Mathf.RoundToInt(flower.BaselineRelaxation * 100)
                : "";
            GUI.Label(new Rect(1340, 44, 530, 240), "BrainLink Pro · F9 显示/隐藏\n"
                + settings.host + ":" + settings.port + " · " + sample.State + "\n"
                + "来源: " + (sample.Mode ?? "unknown") + "\n" + values + mapping, style);
        }

        void OnDisable()
        {
            Client?.Dispose(); Client = null;
        }
        void OnDestroy() => OnDisable();
    }
}
