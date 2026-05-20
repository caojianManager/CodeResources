using FrameWork.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Framework.TimerManager
{
    public class TimerManager : Singleton<TimerManager>, ITimerManager
    {
        private readonly Dictionary<string,UniqueTimer> _timers = new Dictionary<string,UniqueTimer>();

        public UniqueTimer CreateTimer(int intervalSeconds, int totalDurationSeconds, Action<int> intervalCallback = null, Action endCallback = null,int fineness = 1)
        {
            var timer = new UniqueTimer(intervalSeconds, totalDurationSeconds, intervalCallback, endCallback,fineness);
            _timers[timer.Name] = timer;
            return timer;
        }

        public void StartTimer(string timerName)
        {
            if (!_timers.ContainsKey(timerName))
                return;
            _timers[timerName].Start();
        }
        public void StopTimer(string timerName) 
        {
            if(!_timers.ContainsKey(timerName))
                return;
            _timers[timerName].Stop();
        }
        public void StopAllTimer()
        { 
            foreach(var timer in _timers.Values)
            {
                timer.Stop();
            }
        }
    }
}
