using Framework.Event;
using FrameWork.Event;
using FrameWork.MVVM;

namespace EEGTool.ViewModels.Impedance
{
    public class ImpedanceEEGViewModel : BindableBase
    {
        public ImpedanceEEGViewModel()
        {
            EventUtilManager.EventUitl.AddEvent<DataProcessingResult>(
                EventName.RECEVIED_IMPEDANCE_DATA,
                ReceivedData);
        }

        private void ReceivedData(DataProcessingResult result)
        {
        }
    }
}
