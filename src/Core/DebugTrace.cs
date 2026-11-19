#pragma warning disable
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows.Threading;

namespace WinMemoryCleaner
{
    /// <summary>
    /// DEBUG BUILD ONLY. Writes a timestamped trace of every optimization step to
    /// %TEMP%\WinMemoryCleaner-debug.log, and a watchdog that reports any step that
    /// takes longer than 5 seconds and whether the UI thread is still responding.
    /// </summary>
    public static class DebugTrace
    {
        private static readonly object Sync = new object();
        private static readonly string FilePath = Path.Combine(Path.GetTempPath(), "WinMemoryCleaner-debug.log");
        private static string _step;
        private static DateTime _stepStart;
        private static DateTime _uiLastSeen = DateTime.Now;
        private static Dispatcher _uiDispatcher;
        private static Timer _watchdog;

        public static void Init(Dispatcher uiDispatcher)
        {
            _uiDispatcher = uiDispatcher;

            Log("==== DEBUG BUILD started | " + Environment.OSVersion + " | CLR " + Environment.Version +
                " | " + (Environment.Is64BitProcess ? "x64" : "x86") + " | PID " + Process.GetCurrentProcess().Id +
                " | log: " + FilePath + " ====");

            _watchdog = new Timer(OnWatchdog, null, 5000, 5000);
        }

        public static void Begin(string step)
        {
            lock (Sync)
            {
                _step = step;
                _stepStart = DateTime.Now;
            }

            Log("BEGIN " + step);
        }

        public static void End()
        {
            string step;
            double seconds;

            lock (Sync)
            {
                step = _step;
                seconds = (DateTime.Now - _stepStart).TotalSeconds;
                _step = null;
            }

            Log(string.Format("END   {0} ({1:0.000}s)", step, seconds));
        }

        public static void Log(string message)
        {
            try
            {
                var line = string.Format("{0:HH:mm:ss.fff} [T{1,3}] {2}{3}", DateTime.Now, Thread.CurrentThread.ManagedThreadId, message, Environment.NewLine);

                lock (Sync)
                {
                    File.AppendAllText(FilePath, line);
                }
            }
            catch
            {
                // ignored
            }
        }

        private static void OnWatchdog(object state)
        {
            try
            {
                string step;
                double seconds;

                lock (Sync)
                {
                    step = _step;
                    seconds = (DateTime.Now - _stepStart).TotalSeconds;
                }

                if (step != null && seconds > 5)
                    Log(string.Format("!!! STALL: still in '{0}' after {1:0}s", step, seconds));

                // UI thread heartbeat
                var uiSilence = (DateTime.Now - _uiLastSeen).TotalSeconds;

                if (uiSilence > 7)
                    Log(string.Format("!!! UI THREAD NOT RESPONDING for {0:0}s", uiSilence));

                if (_uiDispatcher != null)
                    _uiDispatcher.BeginInvoke((Action)(() => { _uiLastSeen = DateTime.Now; }), DispatcherPriority.Background);
            }
            catch
            {
                // ignored
            }
        }
    }
}
