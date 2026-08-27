using EEGTool.ViewModels.Filters;
using Framework.MVVM.Commands;
using FrameWork.MVVM;
using System;
using System.Windows.Input;
using System.Windows.Threading;

namespace EEGTool.ViewModels.Collection
{
    public class MonitorContainerViewModel : BindableBase
    {
        private const string SingleLayout = "Single";
        private const string TwoLayout = "Two";
        private const string ThreeLayout = "Three";

        private const string EegMonitor = "EEG";
        private const string FftMonitor = "FFT";
        private const string BandPowerMonitor = "BandPower";

        private string _layoutMode = ThreeLayout;
        private string _selectedSingleMonitor = EegMonitor;
        private bool _isLayoutFlyoutOpen;
        private readonly DispatcherTimer _layoutFlyoutCloseTimer;

        public MonitorContainerViewModel()
        {
            _layoutFlyoutCloseTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(3)
            };
            _layoutFlyoutCloseTimer.Tick += (_, _) => CloseLayoutFlyout();

            SetSingleLayoutCommand = new RelayCommand(_ => SetLayoutAndClose(SingleLayout));
            SetTwoLayoutCommand = new RelayCommand(_ => SetLayoutAndClose(TwoLayout));
            SetThreeLayoutCommand = new RelayCommand(_ => SetLayoutAndClose(ThreeLayout));
            ShowLayoutFlyoutCommand = new RelayCommand(_ => ShowLayoutFlyout());
            ClickFilterCommand = new RelayCommand(_ => ClickFilterBtn());
        }

        public ICommand SetSingleLayoutCommand { get; }
        public ICommand SetTwoLayoutCommand { get; }
        public ICommand SetThreeLayoutCommand { get; }
        public ICommand ShowLayoutFlyoutCommand { get; }
        public ICommand ClickFilterCommand { get; }

        public bool IsLayoutFlyoutOpen
        {
            get => _isLayoutFlyoutOpen;
            set => SetProperty(ref _isLayoutFlyoutOpen, value);
        }

        public string SelectedSingleMonitor
        {
            get => _selectedSingleMonitor;
            set
            {
                if (SetProperty(ref _selectedSingleMonitor, value))
                {
                    OnPropertyChanged(nameof(IsSingleEegVisible));
                    OnPropertyChanged(nameof(IsSingleFftVisible));
                    OnPropertyChanged(nameof(IsSingleBandPowerVisible));
                }
            }
        }

        public bool IsSingleLayout => LayoutMode == SingleLayout;
        public bool IsTwoLayout => LayoutMode == TwoLayout;
        public bool IsThreeLayout => LayoutMode == ThreeLayout;

        public bool IsSingleEegVisible => IsSingleLayout && SelectedSingleMonitor == EegMonitor;
        public bool IsSingleFftVisible => IsSingleLayout && SelectedSingleMonitor == FftMonitor;
        public bool IsSingleBandPowerVisible => IsSingleLayout && SelectedSingleMonitor == BandPowerMonitor;

        private void ShowLayoutFlyout()
        {
            IsLayoutFlyoutOpen = true;
            _layoutFlyoutCloseTimer.Stop();
            _layoutFlyoutCloseTimer.Start();
        }

        private void CloseLayoutFlyout()
        {
            _layoutFlyoutCloseTimer.Stop();
            IsLayoutFlyoutOpen = false;
        }

        private void SetLayoutAndClose(string layoutMode)
        {
            LayoutMode = layoutMode;
            CloseLayoutFlyout();
        }

        private void ClickFilterBtn()
        {
            FilterConfigViewModel.Show();
        }

        private string LayoutMode
        {
            get => _layoutMode;
            set
            {
                if (SetProperty(ref _layoutMode, value))
                {
                    OnPropertyChanged(nameof(IsSingleLayout));
                    OnPropertyChanged(nameof(IsTwoLayout));
                    OnPropertyChanged(nameof(IsThreeLayout));
                    OnPropertyChanged(nameof(IsSingleEegVisible));
                    OnPropertyChanged(nameof(IsSingleFftVisible));
                    OnPropertyChanged(nameof(IsSingleBandPowerVisible));
                }
            }
        }
    }
}
