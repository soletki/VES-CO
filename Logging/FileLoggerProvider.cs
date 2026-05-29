using System.IO;
using Microsoft.Extensions.Logging;

namespace VESCO.Logging
{
    public sealed class FileLoggerProvider : ILoggerProvider
    {
        private readonly object _writeLock = new();
        private readonly string _logFilePath;

        public FileLoggerProvider(string logDirectory)
        {
            Directory.CreateDirectory(logDirectory);
            _logFilePath = Path.Combine(logDirectory, $"vesco-{DateTime.Now:yyyyMMdd}.log");
        }

        public string LogFilePath => _logFilePath;

        public ILogger CreateLogger(string categoryName)
        {
            return new FileLogger(categoryName, _logFilePath, _writeLock);
        }

        public void Dispose()
        {
        }

        private sealed class FileLogger : ILogger
        {
            private readonly string _categoryName;
            private readonly string _logFilePath;
            private readonly object _writeLock;

            public FileLogger(string categoryName, string logFilePath, object writeLock)
            {
                _categoryName = categoryName;
                _logFilePath = logFilePath;
                _writeLock = writeLock;
            }

            public IDisposable BeginScope<TState>(TState state) where TState : notnull
            {
                return NullScope.Instance;
            }

            public bool IsEnabled(LogLevel logLevel)
            {
                return logLevel != LogLevel.None;
            }

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                if (!IsEnabled(logLevel))
                {
                    return;
                }

                string message = formatter(state, exception);
                if (string.IsNullOrWhiteSpace(message) && exception == null)
                {
                    return;
                }

                string logEntry =
                    $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz} [{logLevel}] {_categoryName}: {message}{Environment.NewLine}";

                if (exception != null)
                {
                    logEntry += $"{exception}{Environment.NewLine}";
                }

                try
                {
                    lock (_writeLock)
                    {
                        File.AppendAllText(_logFilePath, logEntry);
                    }
                }
                catch
                {
                }
            }
        }

        private sealed class NullScope : IDisposable
        {
            public static NullScope Instance { get; } = new();

            public void Dispose()
            {
            }
        }
    }
}
