using SimpleTCP;
using System.Net;
using System.Text;

namespace FrameWork.NetWork.TCP
{
    public class TCPClient
    {
        private Action<Message>? _delimiterDataReceivedClientAction;
        private Action<Message>? _dataReceivedClientAction;

        private SimpleTcpClient? _simpleTcpClient;


        private void InitClient()
        {
            _simpleTcpClient = new SimpleTcpClient();
            _simpleTcpClient.Delimiter = Encoding.ASCII.GetBytes("\r")[0];
            _simpleTcpClient.StringEncoder = Encoding.UTF8;
            _simpleTcpClient.DelimiterDataReceived += DelimiterDataReceivedClient;
            _simpleTcpClient.DataReceived += DataReceivedClient;
        }
        //启动客户端连接
        public void StartClient(IPAddress ipAddress, int port)
        {
            if (_simpleTcpClient == null)
                InitClient();
            _simpleTcpClient?.Connect(ipAddress.ToString(), port);
        }
        //停止客户端连接
        public void StopClient()
        {
            if (_simpleTcpClient != null)
                _simpleTcpClient.Disconnect();
            _simpleTcpClient = null;
        }
        //向服务器发送数据
        public void SendDataToServer(string data)
        {
            if (_simpleTcpClient != null)
                _simpleTcpClient.Write(data);
        }

        public void SendDataToServer(byte[] data)
        {
            if (_simpleTcpClient != null)
                _simpleTcpClient.Write(data);
        }

        public bool ClientIsConnected()
        {
            if (_simpleTcpClient != null)
                return _simpleTcpClient.IsConnected;
            return false;
        }
        //设置分割数据接收事件-Client
        public void SetDelimiterDataClientReceivedAction(Action<Message> delimiterDataReceivedClientAction)
        {
            _delimiterDataReceivedClientAction += delimiterDataReceivedClientAction;
        }
        //设置数据接收事件-Client
        public void SetDataReceivedClientAction(Action<Message> dataReceivedClientAction)
        {
            _dataReceivedClientAction += dataReceivedClientAction;
        }
        private void DelimiterDataReceivedClient(object sender, Message msg)
        {
            _delimiterDataReceivedClientAction?.Invoke(msg);
        }

        private void DataReceivedClient(object sender, Message msg)
        {
            _dataReceivedClientAction?.Invoke(msg);
        }
    }
}
