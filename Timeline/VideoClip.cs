using System.IO;
using System.Windows.Media.Imaging;
using Xabe.FFmpeg;

namespace VESCO.Timeline
{
    public class VideoClip : Clip
    {
        public VideoClip(string name, double srcStart, double timelineStart, SourceMedia source)
            : base(name, srcStart, timelineStart, source) { }

        public async Task<BitmapImage?> getFrameAt(
            double timelineSeconds,
            double timelineFps
        )
        {
            if (string.IsNullOrEmpty(Source.FilePath))
                return null;

            double clipStart = TimelineStart;
            double clipEnd = TimelineStart + Source.Length;

            if (timelineSeconds < clipStart || timelineSeconds >= clipEnd)
                return null;

            double frameDuration = 1.0 / timelineFps;

            double srcTime = timelineSeconds - clipStart + SourceStart;

            double maxSrcTime = SourceStart + Source.Length - frameDuration;
            srcTime = Math.Max(SourceStart, Math.Min(srcTime, maxSrcTime));

            string tempFrame = Path.Combine(
                Path.GetTempPath(),
                $"{Guid.NewGuid()}.jpg"
            );

            try
            {
                var conversion = Xabe.FFmpeg.FFmpeg.Conversions.New()
                    .AddParameter(
                        $"-ss {srcTime.ToString(System.Globalization.CultureInfo.InvariantCulture)}",
                        ParameterPosition.PreInput
                    )
                    .AddParameter($"-i \"{Source.FilePath}\"")
                    .AddParameter("-vf scale=640:-1")
                    .AddParameter("-vframes 1")
                    .AddParameter("-q:v 3")
                    .AddParameter($"\"{tempFrame}\"", ParameterPosition.PostInput);

                await conversion.Start();
            }
            catch
            {
                return null;
            }

            if (!File.Exists(tempFrame))
                return null;

            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.UriSource = new Uri(tempFrame);
            bmp.EndInit();

            try { File.Delete(tempFrame); } catch { }

            return bmp;
        }


    }
}
