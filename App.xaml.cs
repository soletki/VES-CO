using System.IO;
using System.Windows;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using VESCO.Logging;
using Xabe.FFmpeg.Downloader;

namespace VESCO
{
    public partial class App : Application
    {
        private const string LogLevelEnvironmentVariable = "VESCO_LOG_LEVEL";

        public ILoggerFactory LoggerFactory { get; private set; } = NullLoggerFactory.Instance;
        public string LogFilePath { get; private set; } = string.Empty;

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            ConfigureLogging();

            ILogger<App> logger = LoggerFactory.CreateLogger<App>();
            RegisterGlobalExceptionHandlers(logger);
            logger.LogInformation("Application starting");

            try
            {
                string ffmpegFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "VESCO",
                    "ffmpeg");

                logger.LogInformation("Preparing FFmpeg in {FfmpegFolder}", ffmpegFolder);
                await FFmpegDownloader.GetLatestVersion(FFmpegVersion.Full, ffmpegFolder);
                Xabe.FFmpeg.FFmpeg.SetExecutablesPath(ffmpegFolder);
                logger.LogInformation("FFmpeg is ready");

                MainWindow mainWindow = new MainWindow(LoggerFactory);
                MainWindow = mainWindow;
                mainWindow.Show();
                logger.LogInformation("Main window displayed");
            }
            catch (Exception ex)
            {
                logger.LogCritical(ex, "Application startup failed");
                MessageBox.Show(
                    "VESCO failed to start. Check the log file for details:\n" + LogFilePath,
                    "Startup Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Shutdown(-1);
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            LoggerFactory.Dispose();
            base.OnExit(e);
        }

        private void ConfigureLogging()
        {
            string appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "VESCO");
            string logDirectory = Path.Combine(appDataPath, "logs");

            Directory.CreateDirectory(logDirectory);
            PruneOldLogFiles(logDirectory, maxFilesToKeep: 10);

            FileLoggerProvider fileLoggerProvider = new(logDirectory);
            LogFilePath = fileLoggerProvider.LogFilePath;

            LogLevel minimumLevel = ResolveMinimumLogLevel();
            LoggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder =>
            {
                builder.ClearProviders();
                builder.SetMinimumLevel(minimumLevel);
                builder.AddProvider(fileLoggerProvider);
                builder.AddDebug();
            });
        }

        private static LogLevel ResolveMinimumLogLevel()
        {
            string? configuredLevel = Environment.GetEnvironmentVariable(LogLevelEnvironmentVariable);
            if (!string.IsNullOrWhiteSpace(configuredLevel) &&
                Enum.TryParse(configuredLevel, true, out LogLevel parsedLevel))
            {
                return parsedLevel;
            }

#if DEBUG
            return LogLevel.Debug;
#else
            return LogLevel.Information;
#endif
        }

        private static void PruneOldLogFiles(string logDirectory, int maxFilesToKeep)
        {
            if (!Directory.Exists(logDirectory))
            {
                return;
            }

            FileInfo[] oldLogFiles = new DirectoryInfo(logDirectory)
                .GetFiles("vesco-*.log")
                .OrderByDescending(file => file.CreationTimeUtc)
                .Skip(maxFilesToKeep)
                .ToArray();

            foreach (FileInfo oldLogFile in oldLogFiles)
            {
                try
                {
                    oldLogFile.Delete();
                }
                catch
                {
                }
            }
        }

        private void RegisterGlobalExceptionHandlers(ILogger<App> logger)
        {
            DispatcherUnhandledException += (_, args) =>
            {
                logger.LogError(args.Exception, "Unhandled UI exception");
            };

            AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            {
                if (args.ExceptionObject is Exception exception)
                {
                    logger.LogCritical(exception, "Unhandled non-UI exception");
                }
                else
                {
                    logger.LogCritical("Unhandled non-UI exception: {ExceptionObject}", args.ExceptionObject);
                }
            };

            TaskScheduler.UnobservedTaskException += (_, args) =>
            {
                logger.LogError(args.Exception, "Unobserved task exception");
                args.SetObserved();
            };
        }
    }
}
