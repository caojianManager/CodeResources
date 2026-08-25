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
using System.Threading;
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

        private CameraFrame? _latestCameraFrame;

        public CameraFrame? LatestCameraFrame
        {
            get => _latestCameraFrame;
            set => SetProperty(ref _latestCameraFrame, value);
        }

        private bool _isMagnifierEnabled = true;

        public bool IsMagnifierEnabled
        {
            get => _isMagnifierEnabled;
            set => SetProperty(ref _isMagnifierEnabled, value);
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

        public IReadOnlyList<CameraQualityOption> CameraQualityOptions { get; } =
            new[]
            {
                new CameraQualityOption("1080p 16:9 30fps", 1920, 1080, 30),
                new CameraQualityOption("720p 16:9 30fps", 1280, 720, 30),
                new CameraQualityOption("360p 16:9 30fps", 640, 360, 30),
                new CameraQualityOption("960p 4:3 30fps", 1280, 960, 30),
                new CameraQualityOption("480p 4:3 30fps", 640, 480, 30)
            };

        private CameraQualityOption _selectedCameraQuality;

        public CameraQualityOption SelectedCameraQuality
        {
            get => _selectedCameraQuality;
            set => SetProperty(ref _selectedCameraQuality, value);
        }

        public ICommand? BackHomeCommand { get; set; }
        public ICommand? RecordVideoCommand { get; set; }

        private MediaFoundationCamera? _camera;
        private int _isPreviewUpdateQueued;
        private long _lastPreviewUpdateMilliseconds;

        public YellowSpotHomeViewModel()
        {
            _selectedCameraQuality = CameraQualityOptions[0];
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
                long now = Environment.TickCount64;
                if (now - _lastPreviewUpdateMilliseconds < 33)
                {
                    return;
                }

                _lastPreviewUpdateMilliseconds = now;

                if (Interlocked.Exchange(ref _isPreviewUpdateQueued, 1) == 1)
                {
                    return;
                }

                App.Current.Dispatcher.BeginInvoke(() =>
                {
                    try
                    {
                        LatestCameraFrame = frame;
                    }
                    finally
                    {
                        Interlocked.Exchange(ref _isPreviewUpdateQueued, 0);
                    }
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

            camera.StartCaptureSystemQuality(
                cameraIndex: 0,
                width: SelectedCameraQuality.Width,
                height: SelectedCameraQuality.Height,
                frameRate: SelectedCameraQuality.FrameRate);

            IsRecording = true;
        }

        private void StopCamera()
        {
            _camera?.Stop();
            _camera?.Dispose();
            _camera = null;
            LatestCameraFrame = null;
            IsRecording = false;
            Interlocked.Exchange(ref _isPreviewUpdateQueued, 0);
            _lastPreviewUpdateMilliseconds = 0;
        }

        public void OnHide()
        {

        }

        public void OnShow() 
        {

        }
    }

    public sealed class CameraQualityOption
    {
        public CameraQualityOption(string name, int width, int height, int frameRate)
        {
            Name = name;
            Width = width;
            Height = height;
            FrameRate = frameRate;
        }

        public string Name { get; }

        public int Width { get; }

        public int Height { get; }

        public int FrameRate { get; }
    }
}
