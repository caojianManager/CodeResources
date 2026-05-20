namespace FrameWork.NetWork.Websocket
{
    public enum WebsocketEventEnum
    {
        RECORD_SEND = 1,        //记录开始-结束的消息

        PLAYBACK_START =2,      //回放开始
        PLAYBACK_STOP = 3,      //回放结束

        PLAYBACK_RECIVED = 4,   //回放接收
    }
}
