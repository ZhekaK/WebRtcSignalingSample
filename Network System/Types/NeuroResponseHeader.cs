using FlexNet.Server;

namespace NetworkDTO.Types
{
    public class NeuroResponseHeader
    {
        public ResponseCode ResponseCode { get; set; }

        public override string ToString()
        {
            return ResponseCode.ToString();
        }
    }
}
