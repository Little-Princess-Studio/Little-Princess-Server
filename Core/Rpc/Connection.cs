using LPS.Core.Rpc.Protocal;

namespace LPS.Core.Rpc
{
    public class Connection
    {
        public IProtocal Protocal { get; private set; }

        void ReConnect() { }

        void SendData() { }

        void OnDataRecieved() { }
    }
}
