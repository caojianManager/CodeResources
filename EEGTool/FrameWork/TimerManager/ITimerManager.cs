using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Framework.TimerManager
{
    public interface ITimerManager
    {
        //totalDurationSeconds == -1的时候取int.MaxValue
        UniqueTimer CreateTimer(int intervalSeconds, int totalDurationSeconds, Action<int>? intervalCallback, Action? endCallback, int fineness = 1);
        void StartTimer(string timerName);
        void StopTimer(string timerName);
        void StopAllTimer();
    }
}
