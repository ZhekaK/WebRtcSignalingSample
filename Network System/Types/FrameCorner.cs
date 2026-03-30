using System;

namespace NetworkDTO.Types
{
    [Serializable]
    public struct FrameCorner
    {
        public int X;
        public int Y;

        public FrameCorner(int x, int y)
        {
            X = x;
            Y = y;
        }

        public override string ToString()
        {
            return $"({X}, {Y})";
        }
    }
}
