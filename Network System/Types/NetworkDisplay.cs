using System;
using System.Text;

namespace NetworkDTO.Types
{
    [Serializable]
    public readonly struct NetworkDisplay : IEquatable<NetworkDisplay>
    {
        public readonly int DisplayId;
        public readonly PortType BasePort;
        public readonly int PortForUse => DisplayId + (int)BasePort;

        public enum PortType
        {
            BasePort = 8000,
            ImagePort = BasePort + 0,
            FramePort = BasePort + 10
        }

        public NetworkDisplay(int displayId, PortType basePort)
        {
            DisplayId = displayId;
            BasePort = basePort;
        }

        public override readonly string ToString()
        {
            StringBuilder stringBuilder = new();
            stringBuilder.Append($"{nameof(DisplayId)}: {DisplayId} | ");
            stringBuilder.Append($"{nameof(BasePort)}: {BasePort} | ");
            stringBuilder.Append($"{nameof(PortForUse)}: {PortForUse}");

            return stringBuilder.ToString();
        }

        public override readonly bool Equals(object? obj)
        {
            return obj is NetworkDisplay display && Equals(display);
        }

        public readonly bool Equals(NetworkDisplay other)
        {
            return DisplayId == other.DisplayId && BasePort == other.BasePort;
        }

        public override readonly int GetHashCode()
        {
            return HashCode.Combine(DisplayId, BasePort, PortForUse);
        }

        public static bool operator ==(NetworkDisplay evsDisplay1, NetworkDisplay evsDisplay2)
        {
            return evsDisplay1.Equals(evsDisplay2);
        }

        public static bool operator !=(NetworkDisplay evsDisplay1, NetworkDisplay evsDisplay2)
        {
            return !evsDisplay1.Equals(evsDisplay2);
        }
    }
}
