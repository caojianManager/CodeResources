using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Framework.Event;
using FrameWork.Event;
using FrameWork.MVVM;
using Framework.MVVM.Commands;

namespace EEGTool.ViewModels.Collection
{
    public class EEGMonitorViewModel : BindableBase
    {
        private bool _isAuto = false;
        public bool IsAuto
        {
            get => _isAuto;
            set => SetProperty(ref _isAuto, value);
        }

        public ICommand? EegAutoClickCommand { get; set; }

        public EEGMonitorViewModel()
        {
            Config();
        }

        private void Config()
        {
            EegAutoClickCommand = new RelayCommand((o) =>
            {
                IsAuto = !IsAuto;
            });

            EventUtilManager.EventUitl.AddEvent<DataProcessingResult>(EventName.RECEVIED_COLLECTION_DATA,
                (o) => { ReceviedCollectionData(o);});
        }

        private void ReceviedCollectionData(DataProcessingResult result)
        {

        }
    }
}
