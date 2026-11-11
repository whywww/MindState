using System;

namespace MindState.BCI.BrainLink
{
    [Serializable]
    public sealed class BrainLinkSettings
    {
        public string host = "127.0.0.1";
        public int port = 13856;
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(host) || port < 1 || port > 65535)
                throw new ArgumentException("Invalid BrainLink host or port (1–65535).");
        }
    }

    public struct BrainLinkSnapshot
    {
        public bool Connected;
        public string Status, Mode;
        public int Attention, Meditation, Signal, InvalidPackets;
        public long Revision;
        public double Age;
        public bool HasSample => Revision > 0 && Attention >= 0 && Meditation >= 0 && Signal >= 0;
        public bool Fresh => Connected && HasSample && Age < 5;
        public bool Usable => Fresh && Signal == 0;
        public string State => !Connected ? Status : !HasSample ? "Waiting for complete data"
            : !Fresh ? "Stale (>5s)" : Signal != 0 ? "Poor contact" : "Receiving";
        public string Describe() => State + " | mode: " + (Mode ?? "unknown")
            + (HasSample ? $" | attention: {Attention} | meditation: {Meditation} | signal: {Signal} | age: {Age:F1}s"
                : " | No data / 暂无数据") + " | invalid packets: " + InvalidPackets;
    }
}
