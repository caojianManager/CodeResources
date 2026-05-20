
namespace Framework.Event
{
    public enum EventName
    {
        //切换主窗口页面
        BACK_HOME = 0,

        //根据Type切换页面
        SWITCH_PAGE_WITH_TYPE =1,

        //更新副标题的状态
        UPDATE_SUB_TITLE = 2,

        //更新用户列表内容
        UPDATE_USER_LIST_CONTENT = 3,

        //用户列表内容修改选中用户
        USER_LIST_CONTENT_SELECTED_USER = 4, 
        
        //用户列表内容搜索用户
        USER_LIST_SEARCH_USER = 5,

        //回放记录呢绒修改选中记录
        RECORD_LIST_CONTENT_SELECTED_RECORD = 6,

        //更新记录列表内容
        UPDATE_RECORD_LIST_CONTENT = 7,


        //回放相关
        //发送回放数据
        PLAYBACK_ENGINE_ONDATA = 8,
        PLAYBACK_INIT = 9,
        PLAYBACK_UPDATE_TIMELINE_POSITION = 15,
        PLAYBACK_UPDATE_TIMELINE = 16,
        PLAYBACK_UPDATE_WINDOW_SEC = 17,
        PLAYBACK_DRAGE_SLIDER_VALUE = 18,
        PLAYBACK_UDPATE_CURRENT_FRAME = 26,
       
        //事件标注相关
        EVENT_MARK_ADD_EVENT = 19,
        EVENT_MARK_DELETE = 20,
        EVENT_MARK_VIEW = 21,
        EVENT_TAG_MARK = 27,
        EVENT_TAG_MARK_STATE = 28,

        PLAYBACK_RESET_TIMELINE_POS = 22,
        PLAYBACK_RESET = 23,
        PLAYBACK_CONTENT_STOP = 24,
        PLAYBACK_MONITOR_STOP = 25,
        PLAYBACK_ISCANZOOM= 29,
        PLAYBACK_SET_ALL_CHANNEL_VISIBLE,
        PLAYBACK_SET_SELECTED_CHANNEL_FILTER,
        PLAYBACK_MONITOR_SET_ALL_CHANNEL_VISIBLE,
        PLAYBACK_MONITOR_SET_SELECTED_CHANNEL_FILTER,

        UPDATE_CONFIG_FILE = 999,

        // 波形数据
        SEND_EEG_DATA ,
        // 频谱图数据
        SEND_FFT_DATA,
        // 波段功率数据
        SEND_BAND_POWER_DATA,

        // 停止阻抗检测
        CLOSE_IMPEDANCE,
        // 设置采集流程界面显隐
        SET_ACQUISTION_STEP,

        ACQUISTION_INIT,
        // 开始采集
        START_RECORD,
        //结束采集
        END_RECORD,
        //播放数据
        PLAY_WAVEFORM,
        // 更新记录时间
        UPDATE_RECORD_TIME,

        //阻抗检测值修改
        SET_IMPEDANCE_VALUE,
        //设置采集时事件的状态
        SET_ACQUISTION_EVENT_STATE,
        //标记完成事件
        SET_ACQUISTION_EVENT_END,
        //
        ACQUISTION_ISCANZOOM,
        ACQUISTION_LAYOUT_SWITCH,
        
        ACQUISTION_CHART_TYPE_SWITCH,
        // 回放选区
        PLAYBACK_SELECTION_REGION_CREATED,
        STOP_PROCESSING,
        CHART_TYPE_SWITCH,
        LAYOUT_SWITCH,
        
        //采集时三视图通道显隐控制
        ACQUISTION_SET_ALL_CHANNEL_VISIBLE,
        ACQUISTION_SET_SELECTED_CHANNEL_FILTER,
        //采集时分页式通道显隐控制
        ACQUISTION_MONITOR_SET_ALL_CHANNEL_VISIBLE,
        ACQUISTION_MONITOR_SET_SELECTED_CHANNEL_FILTER,

        BLE_CONNECTED,
        BLE_DISCONNECTED,

    }
}
