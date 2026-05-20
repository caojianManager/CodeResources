using Framework.Event;

namespace FrameWork.Event
{
    public class EventUtilManager
    {
        private static EventUtil<EventName> _eventUtil = new EventUtil<EventName>();
        public static EventUtil<EventName> EventUitl { get { return _eventUtil; } }

    }
}
