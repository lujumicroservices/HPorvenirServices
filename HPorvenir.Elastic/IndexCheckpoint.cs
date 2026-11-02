using Newtonsoft.Json;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace HPorvenir.Elastic
{
    public class IndexCheckpoint
    {
        private const int SaveEveryN = 25;

        private readonly string _checkpointPath;
        private readonly string _failuresPath;
        private readonly HashSet<string> _completedSources;
        private readonly object _lock = new object();
        private string _lastCompletedSource;
        private int _unsavedChanges;

        public IndexCheckpoint(string checkpointPath, string failuresPath)
        {
            _checkpointPath = checkpointPath;
            _failuresPath = failuresPath;
            MigrateLegacyCheckpointIfNeeded();
            _completedSources = LoadCompletedSources();
            _lastCompletedSource = LoadLastCompletedSource();
        }

        public string CheckpointPath => _checkpointPath;

        public string LastCompletedSource
        {
            get
            {
                lock (_lock)
                {
                    return _lastCompletedSource;
                }
            }
        }

        public bool IsCompleted(string sourceKey)
        {
            lock (_lock)
            {
                return _completedSources.Contains(NormalizeKey(sourceKey));
            }
        }

        public void MarkCompleted(IEnumerable<string> sourceKeys)
        {
            lock (_lock)
            {
                var added = 0;
                foreach (var key in sourceKeys)
                {
                    var normalized = NormalizeKey(key);
                    if (_completedSources.Add(normalized))
                    {
                        added++;
                    }

                    _lastCompletedSource = key.Replace('\\', '/');
                }

                if (added == 0)
                {
                    return;
                }

                _unsavedChanges += added;
                if (_unsavedChanges >= SaveEveryN)
                {
                    TrySave();
                }
            }
        }

        public void MarkCompleted(string sourceKey) => MarkCompleted(new[] { sourceKey });

        public void Flush()
        {
            lock (_lock)
            {
                if (_unsavedChanges > 0 || File.Exists(_checkpointPath))
                {
                    TrySave();
                }
            }
        }

        public void RecordFailure(string sourceKey, string error)
        {
            var failuresDirectory = Path.GetDirectoryName(Path.GetFullPath(_failuresPath));
            if (!string.IsNullOrEmpty(failuresDirectory))
            {
                Directory.CreateDirectory(failuresDirectory);
            }

            lock (_lock)
            {
                File.AppendAllText(
                    _failuresPath,
                    JsonConvert.SerializeObject(new
                    {
                        source = sourceKey,
                        error,
                        at = DateTime.UtcNow
                    }) + Environment.NewLine);
            }
        }

        public int CompletedCount
        {
            get
            {
                lock (_lock)
                {
                    return _completedSources.Count;
                }
            }
        }

        private void MigrateLegacyCheckpointIfNeeded()
        {
            if (File.Exists(_checkpointPath))
            {
                return;
            }

            var fileName = Path.GetFileName(_checkpointPath);
            var legacyPath = Path.Combine(Directory.GetCurrentDirectory(), fileName);
            if (!File.Exists(legacyPath))
            {
                return;
            }

            var directory = Path.GetDirectoryName(Path.GetFullPath(_checkpointPath));
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.Copy(legacyPath, _checkpointPath);
            Log.Information("Migrated checkpoint from {LegacyPath} to {CheckpointPath}", legacyPath, _checkpointPath);
        }

        private HashSet<string> LoadCompletedSources()
        {
            if (!File.Exists(_checkpointPath))
            {
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            try
            {
                var state = JsonConvert.DeserializeObject<CheckpointState>(File.ReadAllText(_checkpointPath));
                return new HashSet<string>(
                    state?.CompletedSources ?? Enumerable.Empty<string>(),
                    StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Could not load checkpoint from {Path}; starting fresh", _checkpointPath);
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }
        }

        private string LoadLastCompletedSource()
        {
            if (!File.Exists(_checkpointPath))
            {
                return null;
            }

            try
            {
                var state = JsonConvert.DeserializeObject<CheckpointState>(File.ReadAllText(_checkpointPath));
                if (!string.IsNullOrWhiteSpace(state?.LastCompletedSource))
                {
                    return state.LastCompletedSource;
                }

                return state?.CompletedSources?.LastOrDefault();
            }
            catch
            {
                return null;
            }
        }

        private void TrySave()
        {
            try
            {
                Save();
            }
            catch (IOException ex)
            {
                Log.Warning(ex, "Checkpoint save deferred for {Path}; in-memory progress kept", _checkpointPath);
            }
        }

        private void Save()
        {
            var state = new CheckpointState
            {
                CompletedSources = _completedSources.ToList(),
                LastCompletedSource = _lastCompletedSource,
                LastUpdated = DateTime.UtcNow
            };

            var directory = Path.GetDirectoryName(Path.GetFullPath(_checkpointPath));
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var tempPath = _checkpointPath + ".tmp";
            File.WriteAllText(tempPath, JsonConvert.SerializeObject(state, Formatting.Indented));

            const int maxAttempts = 8;
            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    File.Copy(tempPath, _checkpointPath, overwrite: true);
                    File.Delete(tempPath);
                    _unsavedChanges = 0;
                    return;
                }
                catch (IOException) when (attempt < maxAttempts)
                {
                    Thread.Sleep(40 * attempt);
                }
            }
        }

        private static string NormalizeKey(string sourceKey) =>
            sourceKey.Replace('\\', '/').Trim().ToLowerInvariant();

        private class CheckpointState
        {
            public List<string> CompletedSources { get; set; } = new List<string>();
            public string LastCompletedSource { get; set; }
            public DateTime LastUpdated { get; set; }
        }
    }
}
