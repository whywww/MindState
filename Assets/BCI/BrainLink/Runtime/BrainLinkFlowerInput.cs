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
        long appliedRevision;
        bool enteredExternal;
        float nextLog;
        string previousState, error;

        void OnEnable()
        {
            if (!Application.isPlaying) return;
            appliedRevision = 0; enteredExternal = false; nextLog = 0;
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
            if (flower != null && flower.Ready)
            {
                if (!enteredExternal)
                { flower.WaitForArtBrain(); enteredExternal = true; }
                // Apply each callback once: polling an old value must not refresh the flower's timeout.
                if (sample.Usable && sample.Revision != appliedRevision)
                {
                    flower.SetBrainMetrics(sample.Attention / 100f, sample.Meditation / 100f);
                    appliedRevision = sample.Revision;
                }
            }
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

        // Drawn by FlowerGallery so F9 can also report a disabled component.
        public void DrawDiagnostics(GUIStyle style)
        {
            if (!ShowDiagnostics) return;
            var sample = ReadSnapshot();
            GUI.Box(new Rect(1320, 30, 570, 240), GUIContent.none);
            string values = sample.HasSample
                ? $"专注度 attention: {sample.Attention} / 100\n放松度 meditation: {sample.Meditation} / 100\n佩戴信号 signal: {sample.Signal}（0 为良好）\n距最近数据: {sample.Age:F1}s" + (sample.Fresh ? "" : " · 已过期")
                : "暂无数据";
            GUI.Label(new Rect(1340, 44, 530, 220), "BrainLink Pro · F9 显示/隐藏\n"
                + settings.host + ":" + settings.port + " · " + sample.State + "\n"
                + "来源: " + (sample.Mode ?? "unknown") + "\n" + values, style);
        }

        void OnDisable()
        {
            Client?.Dispose(); Client = null;
            if (enteredExternal && flower != null && flower.Ready)
                flower.WaitForArtBrain();
            if (enteredExternal) Debug.Log("[BrainLink] Disabled; connection released; sliders resume control.", this);
            enteredExternal = false;
        }
        void OnDestroy() => OnDisable();
    }
}
