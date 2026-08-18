using Framework.MVVM.Commands;
using FrameWork.MVVM;
using System.Windows.Input;

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

        public MonitorContainerViewModel()
        {
            SetSingleLayoutCommand = new RelayCommand(_ => LayoutMode = SingleLayout);
            SetTwoLayoutCommand = new RelayCommand(_ => LayoutMode = TwoLayout);
            SetThreeLayoutCommand = new RelayCommand(_ => LayoutMode = ThreeLayout);
            ToggleLayoutFlyoutCommand = new RelayCommand(_ => IsLayoutFlyoutOpen = !IsLayoutFlyoutOpen);
        }

        public ICommand SetSingleLayoutCommand { get; }
        public ICommand SetTwoLayoutCommand { get; }
        public ICommand SetThreeLayoutCommand { get; }
        public ICommand ToggleLayoutFlyoutCommand { get; }

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
