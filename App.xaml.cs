using System.Diagnostics;
using System.IO;
using System.Windows;
using Xabe.FFmpeg.Downloader;

namespace VESCO
{
    public partial class App : Application
    {
        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            string ffmpegFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "VESCO", "ffmpeg"
            );

            await FFmpegDownloader.GetLatestVersion(FFmpegVersion.Full, ffmpegFolder);

            Xabe.FFmpeg.FFmpeg.SetExecutablesPath(ffmpegFolder);
        }
    }
}
