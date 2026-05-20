using log4net;
using Debugger = System.Diagnostics.Debug;

namespace FrameWork.Log
{
    public static class Logger
    {
        private static ILog _log = LogManager.GetLogger("log");

        public static void Debug(string value)
        {
            Debugger.WriteLine($"[Debug]:{value}");
            _log.Debug(value);
        }

        public static void Info(string value)
        {
            Debugger.WriteLine($"[Info]:{value}");
            _log.Info(value);
        }

        public static void Warn(string value)
        {
            Debugger.WriteLine($"[Warn]:{value}");
            _log.Warn(value);
        }

        public static void Fatal(string value)
        {
            Debugger.WriteLine($"[Fatal]:{value}");
            _log.Fatal(value);
        }

        public static void Error(string value)
        {
            Debugger.WriteLine($"[Error]:{value}");
            _log.Error(value);
        }

        public static void ReleaseLog()
        {
            Debugger.WriteLine($"[ReleaseLog]");
            _log.Logger.Repository.Shutdown();
        }

    }
}
