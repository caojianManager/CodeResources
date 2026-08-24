using EEGTool.FrameWork.MediaFoundation;
using EEGTool.Views.Basics;
using Framework.Event;
using Framework.MVVM.Commands;
using FrameWork.Event;
using FrameWork.MVVM;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace EEGTool.ViewModels
{
    public class YellowSpotHomeViewModel : BindableBase,IApplicationContentView
    {

        public string Name => "黄斑变性";
        private bool _isSelected = false;
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        private bool _isInit = false;
        public bool IsInit
        {
            get => _isInit;
            set => SetProperty(ref _isInit, value);
        }

        private ImageSource? _cameraImageSource;

        public ImageSource? CameraImageSource
        {
            get => _cameraImageSource;
            set
            {
                _cameraImageSource = value;
                OnPropertyChanged(nameof(CameraImageSource));
            }
        }

        private string? _cameraErrorMessage;

        public string? CameraErrorMessage
        {
            get => _cameraErrorMessage;
            set => SetProperty(ref _cameraErrorMessage, value);
        }

        private bool _isRecording;

        public bool IsRecording
        {
            get => _isRecording;
            set => SetProperty(ref _isRecording, value);
        }

        public ICommand? BackHomeCommand { get; set; }
        public ICommand? RecordVideoCommand { get; set; }

        private MediaFoundationCamera? _camera;

        public YellowSpotHomeViewModel()
        {
            ConfigureCommands();
        }

        public void Init()
        {

        }

        private void ConfigureCommands()
        {
            if (BackHomeCommand != null)
            {
                return;
            }

            BackHomeCommand = new RelayCommand((o) =>
            {
                EventUtilManager.EventUitl.OnEvent<Type>(EventName.SWITCH_PAGE_WITH_TYPE, typeof(MainViewModel));
            });
            RecordVideoCommand = new RelayCommand((o) =>
            {
                ClickRecordVideoBtn();
            });
        }

        private void ClickRecordVideoBtn()
        {
            if (IsRecording || _camera?.IsRunning == true)
            {
                StopCamera();
                return;
            }

            StartCamera();
        }

        private void StartCamera()
        {
            StopCamera();
            CameraErrorMessage = null;

            var camera = new MediaFoundationCamera();
            _camera = camera;

            camera.FrameArrived += frame =>
            {
                App.Current.Dispatcher.BeginInvoke(() =>
                {
                    CameraImageSource = BitmapSource.Create(
                        frame.Width,
                        frame.Height,
                        96,
                        96,
                        PixelFormats.Bgr32,
                        null,
                        frame.Data,
                        frame.Width * 4);
                });
            };

            camera.CaptureFailed += ex =>
            {
                App.Current.Dispatcher.BeginInvoke(() =>
                {
                    CameraErrorMessage = $"摄像头采集失败：{ex.Message}";
                    StopCamera();
                });
            };

            camera.StartCapture(cameraIndex: 0, width: 1280, height: 720);
            IsRecording = true;
        }

        private void StopCamera()
        {
            _camera?.Stop();
            _camera?.Dispose();
            _camera = null;
            CameraImageSource = null;
            IsRecording = false;
        }

        public void OnHide()
        {

        }

        public void OnShow()
        {

        }
    }
}
