using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Resources
{
    public static class ResourcesImagePath
    {
        private static ImageSource Load(string relativePath) =>
                new BitmapImage(new Uri($"pack://application:,,,/EegAcquisitionSystem;component/{relativePath}", UriKind.Absolute));

        // 对外暴露为静态属性，XAML 可访问
        public static ImageSource STARTUPVIEW_BG = Load("/Resources/images/startupView_bg.png");
        public static ImageSource SENYUE_ICON = Load("/Resources/images/senyue_icon.png");
        public static ImageSource BRAIN_AREA = Load("/Resources/images/brain_area.png");
        public static ImageSource EMPTY_ARROW = Load("/Resources/images/empty_arrow.png");
        public static ImageSource BACK_ICON = Load("/Resources/images/back_icon.png");
        public static ImageSource DELETE_ICON = Load("/Resources/images/delete_icon.png");
        public static ImageSource PATIENT_ICON = Load("/Resources/images/patient_icon.png");
        public static ImageSource USER_ADD = Load("/Resources/images/user_add.png");
        public static ImageSource USER_ICON = Load("/Resources/images/user_icon.png");
        public static ImageSource STEP_LEFT = Load("/Resources/images/step_left.png");
        public static ImageSource STEP_RIGHT = Load("/Resources/images/step_right.png");
        public static ImageSource STEP_MID = Load("/Resources/images/step_mid.png");
        public static ImageSource PLAY_ICON = Load("/Resources/images/play_icon.png");
        public static ImageSource PAUSE_ICON = Load("/Resources/images/pause_icon.png");
        public static ImageSource HOME_COMBOX_ICON = Load("/Resources/images/home_combox_icon.png");
        public static ImageSource HOME_COMBOX_ARROW = Load("/Resources/images/home_combox_arrow.png");
        public static ImageSource LAYOUT_ICON = Load("/Resources/images/layout_icon.png");
        public static ImageSource PRINT_REPORT = Load("/Resources/images/print_report.png");
        public static ImageSource ZOOM_ICON = Load("/Resources/images/zoom_icon.png");
        public static ImageSource VIEW_LAYOUT_ICON = Load("/Resources/images/view_layout_icon.png");
        public static ImageSource SELECTED_ALL_ICON = Load("/Resources/images/selected_all_icon.png");
        public static ImageSource NO_SELECTED_ALL_ICON = Load("/Resources/images/no_selected_all.png");
        public static ImageSource SHOW_ALL_CHANNEL = Load("/Resources/images/show_all_channel.png");
        public static ImageSource HIDE_ALL_CHANNEL = Load("/Resources/images/hide_all_channel.png");
        public static ImageSource HEATMAP_ICON = Load("/Resources/images/heatmap_icon.png");
        public static ImageSource EDIT_ICON = Load("/Resources/images/edit_icon.png");
        public static ImageSource VIDEO_ICON = Load("/Resources/images/video_icon.png");

        // Acquistion 
        public static ImageSource BAND_POWER_ICON = Load("/Resources/images/band_power_icon.png");
        public static ImageSource EEG_ICON = Load("/Resources/images/eeg_icon.png");
        public static ImageSource FFT_ICON = Load("/Resources/images/fft_icon.png");
        public static ImageSource NOTCH_FILTERING_SWITCH_ON = Load("/Resources/images/notch_filtering_switch_on.png");

        public static ImageSource PLAYBACK_REPLAY = Load("/Resources/images/playback_replay.png");
        public static ImageSource PLAYBACK_STOP = Load("/Resources/images/playback_stop.png");
        public static ImageSource EVENT_LIST_ICON = Load("/Resources/images/event_list_icon.png");
        public static ImageSource EVENT_SEARCH = Load("/Resources/images/event_search.png");
        public static ImageSource DATA_EXPORT_ICON = Load("/Resources/images/data_export_Icon.png");

        public static ImageSource PLAYBACK_EEG_AUTO = Load("/Resources/images/playback_eeg_auto.png");
        public static ImageSource PLAYBACK_EEG_AUTO_SELECTED = Load("/Resources/images/playback_eeg_auto_selected.png");

        public static ImageSource NOTCH_FILTERING_SWITCH_OFF = Load("/Resources/images/notch_filtering_switch_off.png");
        public static ImageSource BANDPASS_SWITCH_ON = Load("/Resources/images/bandpass_switch_on.png");
        public static ImageSource BANDPASS_SWITCH_OFF = Load("/Resources/images/bandpass_switch_off.png");
        public static ImageSource EEG_AUTO_ON = Load("/Resources/images/eeg_auto_on.png");
        public static ImageSource EEG_AUTO_OFF = Load("/Resources/images/eeg_auto_off.png");
       

        //ToolBar
        public static ImageSource COLLECTION_TB = Load("/Resources/images/collection_tb.png");
        public static ImageSource RECORD_TB = Load("/Resources/images/record_tb.png");
        public static ImageSource BLE_TB = Load("/Resources/images/ble_tb.png");
        public static ImageSource USER_TB = Load("/Resources/images/user_tb.png");
        public static ImageSource TEMPLATE_TB = Load("/Resources/images/template_tb.png");
        public static ImageSource EVENTS_TB = Load("/Resources/images/events_tb.png");

        //Event Image
        public static ImageSource EVENT_TAG = Load("/Resources/images/event_tag.png");
        public static ImageSource EVENT_COMPLETE = Load("/Resources/images/event_complete.png");
        public static ImageSource EVENT_DELETE = Load("/Resources/images/event_delete.png");
        public static ImageSource WARNING_ICON = Load("/Resources/images/warning_icon.png");
        public static ImageSource EVENT_CREATE = Load("/Resources/images/event_create.png");
        public static ImageSource EVENT_EDIT = Load("/Resources/images/event_edit.png");
        public static ImageSource SUCCESS_TIP_ICON = Load("/Resources/images/success_tip_icon.png");
        public static ImageSource EVENT_ICON = Load("/Resources/images/event_icon.png");

        // Ble Image
        public static ImageSource BLE_REFRESH_TURN = Load("/Resources/images/ble_refresh_turn.png");
        public static ImageSource BLE_ICO = Load("/Resources/images/ble_ico.png");


        //Layout多分布布局
        public static ImageSource LAYOUT_NONE_OFF = Load("/Resources/images/layout_none_off.png");
        public static ImageSource LAYOUT_NONE_ON = Load("/Resources/images/layout_none_on.png");
        public static ImageSource LAYOUT_TWO_VERTICAL_OFF = Load("/Resources/images/layout_two_vertical_off.png");
        public static ImageSource LAYOUT_TWO_VERTICAL_ON = Load("/Resources/images/layout_two_vertical_on.png");
        public static ImageSource LAYOUT_THREE_VERTICAL_OFF = Load("/Resources/images/layout_three_vertical_off.png");
        public static ImageSource LAYOUT_THREE_VERTICAL_ON = Load("/Resources/images/layout_three_vertical_on.png");

        public static ImageSource ACPQUISTION_IMEDNCE = Load("/Resources/images/acpquistion_imednce.png");
        public static ImageSource ACPQUISTION_RECORD = Load("/Resources/images/acpquistion_record.png");

    }
}
