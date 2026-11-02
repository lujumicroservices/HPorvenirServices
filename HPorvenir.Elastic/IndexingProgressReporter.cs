using Serilog;
using System;
using System.IO;
using System.Threading;

namespace HPorvenir.Elastic
{
    public class IndexingProgressReporter : IDisposable
    {
        private readonly object _lock = new object();
        private string _label = "";
        private int _totalInRun;
        private int _alreadyCompleted;
        private int _sessionOk;
        private int _sessionFailed;
        private int _activeWorkers;
        private string _currentFile = "";
        private DateTime _sessionStart;
        private Timer _heartbeatTimer;
        private bool _heartbeatEnabled;

        public void Begin(string label, int pendingCount, int alreadyCompleted, int heartbeatIntervalSeconds = 0)
        {
            lock (_lock)
            {
                _label = label;
                _totalInRun = pendingCount;
                _alreadyCompleted = alreadyCompleted;
                _sessionOk = 0;
                _sessionFailed = 0;
                _activeWorkers = 0;
                _currentFile = "";
                _sessionStart = DateTime.UtcNow;
                _heartbeatEnabled = heartbeatIntervalSeconds > 0;
            }

            Emit(
                $"{label}: resuming — {_alreadyCompleted} done, {pendingCount} remaining ({_alreadyCompleted + pendingCount} total)");

            if (heartbeatIntervalSeconds > 0)
            {
                var interval = TimeSpan.FromSeconds(heartbeatIntervalSeconds);
                _heartbeatTimer = new Timer(_ => SafeHeartbeat(), null, interval, interval);
            }
        }

        public void WorkerStarted() => Interlocked.Increment(ref _activeWorkers);

        public void WorkerFinished() => Interlocked.Decrement(ref _activeWorkers);

        public void SetCurrentFile(string blobPath)
        {
            lock (_lock)
            {
                _currentFile = Path.GetFileName(blobPath.Replace('\\', '/'));
            }
        }

        public void RecordSuccess()
        {
            Interlocked.Increment(ref _sessionOk);
            if (!_heartbeatEnabled)
            {
                Report(inline: true);
            }
        }

        public void RecordFailure()
        {
            Interlocked.Increment(ref _sessionFailed);
            if (!_heartbeatEnabled)
            {
                Report(inline: true);
            }
        }

        public void Complete()
        {
            _heartbeatTimer?.Dispose();
            _heartbeatTimer = null;

            lock (_lock)
            {
                var total = _alreadyCompleted + _totalInRun;
                var elapsed = DateTime.UtcNow - _sessionStart;
                Emit(
                    $"{_label}: finished — {total}/{total} | this run: {_sessionOk} ok, {_sessionFailed} failed | {FormatDuration(elapsed)}");
            }
        }

        public void Dispose() => _heartbeatTimer?.Dispose();

        private void SafeHeartbeat()
        {
            try
            {
                Heartbeat();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Progress heartbeat failed for {Label}", _label);
            }
        }

        private void Heartbeat() => Report(inline: false);

        private void Report(bool inline)
        {
            string line;
            lock (_lock)
            {
                if (_totalInRun == 0)
                {
                    return;
                }

                line = BuildStatusLine();
            }

            if (inline)
            {
                WriteInline(line);
            }
            else
            {
                Emit(line);
            }
        }

        private string BuildStatusLine()
        {
            var sessionDone = _sessionOk + _sessionFailed;
            var overallDone = _alreadyCompleted + sessionDone;
            var overallTotal = _alreadyCompleted + _totalInRun;
            var percent = overallTotal > 0 ? overallDone * 100.0 / overallTotal : 100;
            var elapsed = DateTime.UtcNow - _sessionStart;
            var rate = sessionDone > 0 && elapsed.TotalSeconds > 0
                ? sessionDone / elapsed.TotalSeconds
                : 0;
            var remaining = _totalInRun - sessionDone;
            var eta = rate > 0 ? TimeSpan.FromSeconds(remaining / rate) : TimeSpan.Zero;
            var workers = Volatile.Read(ref _activeWorkers);

            return
                $"{_label} {overallDone}/{overallTotal} ({percent:F1}%) | " +
                $"workers {workers} | ok {_alreadyCompleted + _sessionOk} fail {_sessionFailed} | " +
                $"{rate:F1}/s | ETA {FormatDuration(eta)} | {_currentFile}";
        }

        public static void Emit(string message)
        {
            Log.Information("{Message}", message);
        }

        private static void WriteInline(string line)
        {
            try
            {
                var stamped = $"[{NowStamp()}] {line}";
                var width = Math.Max(Console.WindowWidth, 140);
                if (stamped.Length >= width)
                {
                    stamped = stamped.Substring(0, width - 4) + "...";
                }

                Console.Write("\r" + stamped.PadRight(width - 1));
                Console.Out.Flush();
            }
            catch
            {
                // ignored
            }
        }

        private static string NowStamp() => DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        private static string FormatDuration(TimeSpan duration)
        {
            if (duration.TotalHours >= 1)
            {
                return $"{(int)duration.TotalHours}h {duration.Minutes}m";
            }

            if (duration.TotalMinutes >= 1)
            {
                return $"{duration.Minutes}m {duration.Seconds}s";
            }

            return $"{Math.Max(0, duration.Seconds)}s";
        }
    }
}
