using FrameWork.Common;
using FrameWork.UserControls.ToastViewControl;
using SimpleTCP;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace FrameWork.NetWork.TCP
{
    public class TCPServer:Singleton<TCPServer>
    {
        //Server Event;
        private Action<Message>? _delimiterDataReceivedServerAction;
        private Action<Message>? _dataReceivedServerAction;
        private Action<TcpClient>? _clientConnectedServerAction;
        private Action<TcpClient>? _clientDisconnectedServerAction;

        private SimpleTcpServer? _simpleTcpServer;

        //初始化服务器
        public void InitServer()
        {
            _simpleTcpServer = new SimpleTcpServer();
            _simpleTcpServer.Delimiter = Encoding.ASCII.GetBytes("\r")[0]; ;
            _simpleTcpServer.StringEncoder = Encoding.UTF8;
            _simpleTcpServer.DelimiterDataReceived += DelimiterDataReceivedServer;    //分割数据接收事件
            _simpleTcpServer.DataReceived += DataReceivedServer;
            _simpleTcpServer.ClientConnected += ClientConnectedServer;
            _simpleTcpServer.ClientDisconnected += ClientDisconnectedServer;
        }

        public void StartServer(IPAddress ipAddress, int port)
        {
            try
            {
                _simpleTcpServer?.Start(ipAddress, port);
            }
            catch (Exception ex) {

                ToastManager.Show($"初始化服务失败{ex.Message}");
            }
        }

        public void SendDataToClient(string data)
        {
            if (_simpleTcpServer != null)
                _simpleTcpServer.Broadcast(data);
        }

        private void DelimiterDataReceivedServer(object sender, Message msg)
        {
            _delimiterDataReceivedServerAction?.Invoke(msg);
        }

        private void DataReceivedServer(object sender, Message msg)
        {
            _dataReceivedServerAction?.Invoke(msg);
        }

        private void ClientConnectedServer(object sender, TcpClient tcpClient)
        {
            _clientConnectedServerAction?.Invoke(tcpClient);
        }

        private void ClientDisconnectedServer(object sender, TcpClient tcpClient)
        {
            _clientDisconnectedServerAction?.Invoke(tcpClient);
        }

    }
}
