using FrameWork.Common;
using FrameWork.Log;
using Websocket.Client;
using System.Text.Json;
using Newtonsoft.Json;
using JsonSerializer = System.Text.Json.JsonSerializer;
using Framework.Event;
using System.Reactive.Linq;

namespace FrameWork.NetWork.Websocket
{
    public class WSMessage
    {
        public string Type { get; set; } = string.Empty;
        public Object Content { get; set; } = new Object();
    }

    public class WebsocketManager : Singleton<WebsocketManager>,IDisposable
    {
        private EventUtil<WebsocketEventEnum> _eventUtil = new EventUtil<WebsocketEventEnum>();
        public EventUtil<WebsocketEventEnum> EventUtil = new EventUtil<WebsocketEventEnum>();

        private Uri _serverUri = new Uri($"ws://{Config.Instance.HTTP_IP}:{Config.Instance.HTTP_PORT}/winapp/ws");
        private WebsocketClient? _client;
        private readonly TimeSpan _reconnectInterval = TimeSpan.FromSeconds(100);
        private IDisposable? _subscription;
        private bool _isDisposed = false;

        public WebsocketManager() {
            
        }

        public static void SendMessage(WSMessage message)
        {
            _= WebsocketManager.GetInstance().SendAsync(message);
        }

        public void Init()
        {
            try
            {
                _serverUri = new Uri($"ws://{Config.Instance.HTTP_IP}:{Config.Instance.HTTP_PORT}/winapp/ws?token={HttpParameter.Authorization}");
            }
            catch (Exception ex)
            {
                Logger.Info("[WebsocketManager-Init]:解析_severUri失败");
            }
            _ = ConnectAsync();
        }

        public async Task ConnectAsync()
        {
            if (_client != null && _client.IsRunning) {
                return;
            }
      
            _client = new WebsocketClient(_serverUri)
            {
                ReconnectTimeout = _reconnectInterval,
                IsReconnectionEnabled = true,
                
            };

            _client.ReconnectionHappened.Subscribe(info =>
            {
                Logger.Info($"[WS] Reconnected:{info.Type}");
            });

            _client.DisconnectionHappened.Subscribe(info => {

                Logger.Info($"[WS] Disconnected:{info.Type}");

                if (info.Exception != null) {

                    Logger.Info($"[WS] Error:{info.Exception.Message}");
                }
            });

            _subscription = _client.MessageReceived.Where(msg => !string.IsNullOrEmpty(msg.Text))
                .Subscribe(msg => {

                    ReceiveMessages(msg);
                });

            await _client.Start();
            if (_client.IsRunning)
            {
                Logger.Info($"Websocket is Running:{_client.Url.AbsoluteUri}");
            }
      
        }

        private async Task SendAsync(WSMessage message)
        {
            if(_client == null || !_client.IsRunning)
            {
                Logger.Info("Websocket is not connected");
                return;
            }
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false,
            };
            string msgJson = JsonSerializer.Serialize(message, options);
            Logger.Info($"[WS]-SendMessage:{msgJson}");
            await _client.SendInstant(msgJson);
        }

        private void ReceiveMessages(ResponseMessage message)
        {
            var reciveText = message?.Text ?? "";
            Logger.Info($"[WS]-ReceiveMessages:{reciveText}");
            var wSMessage = JsonConvert.DeserializeObject<WSMessage>(reciveText);
            if (wSMessage != null) { 
                HandlerReceivedMessage(wSMessage.Type, wSMessage.Content);
            }
        }

        private void HandlerReceivedMessage(string messageType, Object data)
        {
            switch (messageType)
            {
                case "winapp-record-receive": //记录开始-暂停的回复
                    EventUtil.OnEvent<Object>(WebsocketEventEnum.RECORD_SEND, data);
                    return;
                case "winapp-playback-receive":
                    EventUtil.OnEvent<Object>(WebsocketEventEnum.PLAYBACK_RECIVED,data);
                    return;
                case "winapp-playback-finished":
                    string dataStr = data as string;
                    if (dataStr != null && dataStr.Equals("playback_start"))
                    {
                        //回放开始
                        EventUtil.OnEvent(WebsocketEventEnum.PLAYBACK_START);
                    }
                    if (dataStr != null && dataStr.Equals("playback_finished"))
                    {
                        //回放结束
                        EventUtil.OnEvent(WebsocketEventEnum.PLAYBACK_STOP);
                    }
                    return;
            }
            return;
        }

        public void Dispose()
        {
            if (_isDisposed) return;

            _subscription?.Dispose();
            _client?.Dispose();
            _isDisposed = true;
        }
    }
}
