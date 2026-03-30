using System;
using System.Text;

namespace NetworkDTO.Types
{
    [Serializable]
    public class RecognitionObject
    {
        public readonly RecognitionClass RecognitionClass;
        public readonly float Probability;
        public FrameData FrameData;

        public RecognitionObject(RecognitionClass objectClass, float probability, FrameData frame)
        {
            RecognitionClass = objectClass;
            Probability = probability;
            FrameData = frame;
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine(RecognitionClass.ToString());
            sb.AppendLine(Probability.ToString());
            sb.AppendLine(FrameData.ToString());
            return sb.ToString();
        }
    }
}
