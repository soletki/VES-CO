using NAudio.Wave;
using System.Diagnostics;
using System.IO;
using System.Windows.Media.Imaging;

namespace VESCO
{
    public class VideoRenderer
    {
        public event Action<double> ?OnProgress;

        public async Task RenderVideo(
            Timeline.Timeline timeline,
            string outputPath,
            int width,
            int height,
            double fps,
            string codec,
            int quality,
            string pixelFormat,
            CancellationToken cancellationToken = default)
        {
            double originalScale = timeline.PreviewScale;
            timeline.PreviewScale = 1.0;

            long totalFrames = timeline.GetTotalFrames();
            long totalOutputFrames = (long)(totalFrames * (fps / timeline.Fps));

            string tempDir = Path.Combine(Path.GetTempPath(), "vesco_render_" + Guid.NewGuid());
            Directory.CreateDirectory(tempDir);

            try
            {
                await Task.Run(() =>
                {
                    for (long frame = 0; frame < totalOutputFrames; frame++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        long outFrameIndex = (long)(frame * (timeline.Fps / fps));
                        var bitmapSource = timeline.GetFrameAtFrame(outFrameIndex);

                        if (bitmapSource != null)
                        {
                            string framePath = Path.Combine(tempDir, $"frame_{frame:D6}.png");
                            SaveBitmapSourceAsPng(bitmapSource, framePath);
                        }

                        OnProgress?.Invoke((frame + 1.0) / totalOutputFrames * 90.0);
                    }
                }, cancellationToken);

                cancellationToken.ThrowIfCancellationRequested();

                await EncodeWithFFmpeg(tempDir, outputPath, fps, codec, quality, pixelFormat, width, height, cancellationToken, timeline);
            }
            finally
            {
                timeline.PreviewScale = originalScale;

                try
                {
                    Directory.Delete(tempDir, true);
                }
                catch { /* Ignore cleanup errors */ }
            }
        }

        private async Task EncodeWithFFmpeg(
            string tempDir,
            string outputPath,
            double fps,
            string codec,
            int quality,
            string pixelFormat,
            int width,
            int height,
            CancellationToken cancellationToken,
            Timeline.Timeline timeline)
        {
            string inputPattern = Path.Combine(tempDir, "frame_%06d.png");
            string scale = $"-vf scale={width}:{height}";

            string audioPath = Path.Combine(tempDir, $"audio_{Guid.NewGuid()}.wav");
            ExportAudio(timeline, audioPath, fps, cancellationToken);

            var startInfo = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = $"-framerate {fps} -i \"{inputPattern}\" " +
                           $"-i \"{audioPath}\" " +
                           $"{scale} " +
                           $"-c:v {codec} " +
                           $"-c:a aac " +
                           $"-b:a 192k " +
                           $"-pix_fmt {pixelFormat} " +
                           $"-crf {quality} " +
                           $"-y \"{outputPath}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = startInfo };

            process.ErrorDataReceived += (sender, args) =>
            {
                if (args.Data != null && args.Data.Contains("frame="))
                {
                    var parts = args.Data.Split(["frame="], StringSplitOptions.None);
                    if (parts.Length > 1)
                    {
                        var framePart = parts[1].Split(' ')[0].Trim();
                        if (int.TryParse(framePart, out int currentFrame))
                        {
                            var files = Directory.GetFiles(tempDir, "frame_*.png");
                            double progress = 90.0 + (currentFrame / (double)files.Length * 10.0);
                            OnProgress?.Invoke(Math.Min(100, progress));
                        }
                    }
                }
            };

            process.Start();
            process.BeginErrorReadLine();
            using (cancellationToken.Register(() =>
            {
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill();
                    }
                }
                catch { /* Process might have already exited */ }
            }))
            {
                await process.WaitForExitAsync(cancellationToken);
            }

            if (cancellationToken.IsCancellationRequested)
            {
                try
                {
                    if (File.Exists(outputPath))
                    {
                        File.Delete(outputPath);
                    }
                }
                catch { /* Ignore cleanup errors */ }

                throw new OperationCanceledException();
            }

            if (process.ExitCode != 0)
            {
                throw new Exception($"FFmpeg encoding failed with exit code {process.ExitCode}");
            }

            OnProgress?.Invoke(100);
        }

        private void SaveBitmapSourceAsPng(BitmapSource bitmap, string path)
        {
            using var fileStream = new FileStream(path, FileMode.Create);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            encoder.Save(fileStream);
        }

        private static void ExportAudio(Timeline.Timeline timeline, string outputPath, double fps, CancellationToken ct)
        {
            long totalFrames = timeline.GetTotalFrames();
            int sampleRate = 48000;
            int channels = 2;

            using var writer = new WaveFileWriter(outputPath, new WaveFormat(sampleRate, channels));

            for (long frame = 0; frame < totalFrames; frame++)
            {
                ct.ThrowIfCancellationRequested();

                var audioSamples = timeline.GetAudioAtFrame(frame, sampleRate, channels);

                if (audioSamples != null)
                {
                    writer.WriteSamples(audioSamples, 0, audioSamples.Length);
                }
                else
                {
                    int samplesPerFrame = (int)(sampleRate / fps * channels);
                    writer.WriteSamples(new float[samplesPerFrame], 0, samplesPerFrame);
                }
            }
        }
    }
}