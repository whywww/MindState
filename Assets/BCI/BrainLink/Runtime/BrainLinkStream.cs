using System;
using System.Text;
using UnityEngine;

namespace MindState.BCI.BrainLink
{
    // Complete callback snapshots only. Missing fields must never become implicit zeroes.
    public sealed class BrainLinkStream
    {
        [Serializable] private sealed class Packet
        {
            public double attention = double.MinValue;
            public double meditation = double.MinValue;
            public double signal = double.MinValue;
            public string mode;
        }

        private readonly StringBuilder pending = new StringBuilder();
        private bool discarding;
        private double sampleAt = double.NegativeInfinity;
        public int Attention { get; private set; } = -1;
        public int Meditation { get; private set; } = -1;
        public long Revision { get; private set; }
        public int Signal { get; private set; } = -1;
        public string Mode { get; private set; } = "unknown";
        public int InvalidPackets { get; private set; }
        public double Age(double now) => now - sampleAt;

        public void Reset()
        {
            pending.Clear(); discarding = false;
            Invalidate(); Mode = "unknown";
        }

        private void Invalidate()
        {
            Attention = Meditation = Signal = -1;
            sampleAt = double.NegativeInfinity;
        }

        public void Feed(byte[] bytes, int count, double now)
        {
            for (int i = 0; i < count; i++)
            {
                char c = (char)bytes[i];
                if (c == '\n')
                {
                    if (!discarding && pending.Length > 0) Parse(pending.ToString(), now);
                    pending.Clear(); discarding = false;
                }
                else if (c != '\r' && !discarding)
                {
                    if (pending.Length >= 16384)
                    { pending.Clear(); discarding = true; InvalidPackets++; Invalidate(); }
                    else pending.Append(c);
                }
            }
        }

        private void Parse(string json, double now)
        {
            try
            {
                json = json.Trim();
                if (!json.StartsWith("{", StringComparison.Ordinal) || !json.EndsWith("}", StringComparison.Ordinal))
                    throw new ArgumentException("Not an object");
                var packet = new Packet();
                JsonUtility.FromJsonOverwrite(json, packet);
                if (!IntegerInRange(packet.attention, 0, 100) || !IntegerInRange(packet.meditation, 0, 100) || !IntegerInRange(packet.signal, 0, 200)
                    || (packet.mode != "simulate" && packet.mode != "serial"))
                    throw new ArgumentException("Not a complete EEG callback");
                Attention = (int)packet.attention; Meditation = (int)packet.meditation; Revision++; Signal = (int)packet.signal;
                Mode = packet.mode; sampleAt = now;
            }
            catch (ArgumentException) { InvalidPackets++; Invalidate(); }
        }

        private static bool IntegerInRange(double value, int min, int max)
            => value >= min && value <= max && value == Math.Truncate(value);

        public BrainLinkSnapshot Snapshot(bool connected, string status, double now)
            => new BrainLinkSnapshot { Connected=connected, Status=status, Attention=Attention,
                Meditation=Meditation, Signal=Signal, Mode=Mode, Age=Age(now), Revision=Revision,
                InvalidPackets=InvalidPackets };
    }
}
