using System.Collections.Concurrent;


namespace FrameWork.Common
{
    public class ResponseDispatcherUtilKey
    {
        public static string GetDrugOutDrawer = "GetDrugOutDrawer";

        public static string PutDrugInDrawer = "PutDrugInDrawer";

        public static string RecylingInDrawer = "RecylingInDrawer";
    }

    public class ResponseDispatcherUtil
    {
        private readonly ConcurrentDictionary<string, TaskCompletionSource<string>> _waitMap = new ConcurrentDictionary<string, TaskCompletionSource<string>>();

        public Task<string> WaitFor(string key)
        {
            var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            _waitMap[key] = tcs;
            return tcs.Task;
        }

        public void PushFrame(string key, string frame)
        {
            if (key.Length > 0 && _waitMap.TryRemove(key, out var tcs))
            {
                tcs.TrySetResult(frame);
                return;
            }
        }
    }
}
