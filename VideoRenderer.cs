using NAudio.Wave;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using System.Diagnostics;
using System.IO;
using System.Windows.Media.Imaging;

namespace VESCO
{
    public class VideoRenderer
    {
        public event Action<double> OnProgress;

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
            // Set full quality for rendering
            double originalScale = timeline.PreviewScale;
            timeline.PreviewScale = 1.0;

            long totalFrames = timeline.GetTotalFrames();

            // Create temp directory for frames
            string tempDir = Path.Combine(Path.GetTempPath(), "vesco_render_" + Guid.NewGuid());
            Directory.CreateDirectory(tempDir);

            try
            {
                // Step 1: Export all frames as images
                await Task.Run(() =>
                {
                    for (long frame = 0; frame < totalFrames; frame++)
                    {
                        // Check for cancellation
                        cancellationToken.ThrowIfCancellationRequested();

                        var bitmapSource = timeline.GetFrameAtFrame(frame);

                        if (bitmapSource != null)
                        {
                            // Save frame as PNG
                            string framePath = Path.Combine(tempDir, $"frame_{frame:D6}.png");
                            SaveBitmapSourceAsPng(bitmapSource, framePath);
                        }

                        // Report progress (50% for frame export)
                        OnProgress?.Invoke((frame + 1.0) / totalFrames * 50.0);
                    }
                }, cancellationToken);

                // Check cancellation before encoding
                cancellationToken.ThrowIfCancellationRequested();

                // Step 2: Use FFmpeg to encode frames into video
                await EncodeWithFFmpeg(tempDir, outputPath, fps, codec, quality, pixelFormat, width, height, cancellationToken, timeline);
            }
            finally
            {
                // Restore original scale
                timeline.PreviewScale = originalScale;

                // Cleanup temp files
                try
                {
                    Directory.Delete(tempDir, true);
                }
                catch { /* Ignore cleanup errors */ }
            }
        }

        private async Task EncodeWithFFmpeg(
            string framesDir,
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
            string inputPattern = Path.Combine(framesDir, "frame_%06d.png");
            string scale = $"-vf scale={width}:{height}";

            string audioPath = Path.Combine(Path.GetTempPath(), $"audio_{Guid.NewGuid()}.wav");
            ExportAudio(timeline, audioPath, fps, cancellationToken);

            var startInfo = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = $"-framerate {fps} -i \"{inputPattern}\" " +
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

            // Track encoding progress
            process.ErrorDataReceived += (sender, args) =>
            {
                if (args.Data != null && args.Data.Contains("frame="))
                {
                    // Parse frame number from FFmpeg output
                    // Example: "frame= 120 fps=30 q=28.0 size= 256kB time=00:00:04.00"
                    var parts = args.Data.Split(["frame="], StringSplitOptions.None);
                    if (parts.Length > 1)
                    {
                        var framePart = parts[1].Split(' ')[0].Trim();
                        if (int.TryParse(framePart, out int currentFrame))
                        {
                            // Get total frames from input
                            var files = Directory.GetFiles(framesDir, "frame_*.png");
                            double progress = 50.0 + (currentFrame / (double)files.Length * 50.0);
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
                    // Write silence
                    int samplesPerFrame = (int)(sampleRate / fps * channels);
                    writer.WriteSamples(new float[samplesPerFrame], 0, samplesPerFrame);
                }
            }
        }

        public async Task RenderVideoOpenCV(
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
            // Set full quality for rendering
            double originalScale = timeline.PreviewScale;
            timeline.PreviewScale = (double)(52-quality)/51;

            long totalFrames = timeline.GetTotalFrames();

            long totalOutputFrames = (long)(totalFrames * (fps / timeline.Fps));

            await Task.Run(() =>
            {
                try
                {
                    int fourcc = GetFourCC(codec);

                    using var writer = new VideoWriter(
                        outputPath,
                        fourcc,
                        fps,
                        new Size(width, height));

                    if (!writer.IsOpened())
                    {
                        throw new Exception("Failed to open video writer. Make sure the codec is supported.");
                    }

                    for (long frame = 0; frame < totalOutputFrames; frame++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        long outFrameIndex = (long)(frame * (timeline.Fps / fps));
                        var bitmapSource = timeline.GetFrameAtFrame(outFrameIndex);

                        if (bitmapSource != null)
                        {
                            using var mat = BitmapSourceConverter.ToMat(bitmapSource);

                            Mat frameToWrite = mat;
                            if (mat.Channels() == 1)
                            {
                                frameToWrite = new Mat();
                                Cv2.CvtColor(mat, frameToWrite, ColorConversionCodes.GRAY2BGR);
                            }
                            else if (mat.Channels() == 4)
                            {
                                frameToWrite = new Mat();
                                Cv2.CvtColor(mat, frameToWrite, ColorConversionCodes.BGRA2BGR);
                            }

                            if (frameToWrite.Width != width || frameToWrite.Height != height)
                            {
                                using var resized = new Mat();
                                Cv2.Resize(frameToWrite, resized, new Size(width, height));
                                writer.Write(resized);
                            }
                            else
                            {
                                writer.Write(frameToWrite);
                            }

                            if (frameToWrite != mat)
                            {
                                frameToWrite.Dispose();
                            }
                        }

                        OnProgress?.Invoke((frame + 1.0) / totalOutputFrames * 100.0);
                    }

                    writer.Release();
                }
                finally
                {
                    timeline.PreviewScale = originalScale;
                }
            }, cancellationToken);
        }

        private int GetFourCC(string codec)
        {
            return codec switch
            {
                "libx264" => VideoWriter.FourCC('H', '2', '6', '4'), // H.264
                "libx265" => VideoWriter.FourCC('H', '2', '6', '5'), // H.265/HEVC
                "libvpx-vp9" => VideoWriter.FourCC('V', 'P', '9', '0'), // VP9
                "mpeg4" => VideoWriter.FourCC('M', 'P', '4', 'V'), // MPEG-4
                "mjpeg" => VideoWriter.FourCC('M', 'J', 'P', 'G'), // Motion JPEG
                _ => VideoWriter.FourCC('a', 'v', 'c', '1') // Default to H.264
            };
        }
    }
}