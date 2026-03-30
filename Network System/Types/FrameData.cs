using System;
using System.Text;

namespace NetworkDTO.Types
{
    [Serializable]
    public struct FrameData
    {
        public FrameCorner Corner1;
        public FrameCorner Corner2;
        public FrameCorner Corner3;
        public FrameCorner Corner4;

        public FrameData(FrameCorner corner1, FrameCorner corner2, FrameCorner corner3, FrameCorner corner4)
        {
            Corner1 = corner1;
            Corner2 = corner2;
            Corner3 = corner3;
            Corner4 = corner4;
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine(Corner1.ToString());
            sb.AppendLine(Corner2.ToString());
            sb.AppendLine(Corner3.ToString());
            sb.AppendLine(Corner4.ToString());
            return sb.ToString();
        }
    }
}