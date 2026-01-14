using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace VESCO.Timeline
{
    public class VideoClip : Clip
    {
        private VideoCapture _capture;
        private long _lastFrame = -1;

        public int x = 0;
        public int y = 0;
        public double scale = 1.0;
        public double opacity = 1.0;

        public SourceMedia source { get; set; }
        public long sourceStart { get; set; }
        public long length { get; set; }

        public VideoClip(string filePath, long sourceStart, long timelineStart, SourceMedia source, long length = -1, int x = 0, int y = 0, double scale = 1.0, double opacity = 1.0)
            : base(filePath, timelineStart)
        {
            _capture = new VideoCapture(source.FilePath);
            this.source = source;
            this.sourceStart = sourceStart;
            this.x = x;
            this.y = y;
            this.scale = scale;
            this.opacity = opacity;
            if(length != -1)
                this.length = length;
            else
                this.length = source.FrameCount - sourceStart;
        }

        public BitmapSource GetFrameAtTimelineFrame(long timelineFrame, double fps, double scale)
        {
            double fpsRatio = source.FPS / fps;
            long localFrame = sourceStart +
                (long)Math.Round((timelineFrame - timelineStart) * fpsRatio);

            localFrame = Math.Clamp(localFrame, 0, source.FrameCount - 1);

            if (_lastFrame != localFrame - 1)
            {
                _capture.Set(VideoCaptureProperties.PosFrames, localFrame);
            }

            using var mat = new Mat();
            if (!_capture.Read(mat) || mat.Empty())
                return null;

            _lastFrame = localFrame;

            Cv2.Resize(mat, mat, new Size(mat.Width * scale, mat.Height * scale), 0, 0, InterpolationFlags.Area);

            return mat.ToBitmapSource();
        }


        public (VideoClip? first, VideoClip? second) SplitAtFrame(long splitFrame, Timeline timeline)
        {
            if (splitFrame <= 0 || splitFrame >= this.length)
                return (this, null);

            var firstClip = new VideoClip(
                filePath,
                sourceStart,
                timelineStart,
                new SourceMedia(source.FilePath),
                length: splitFrame,
                x,
                y,
                scale,
                opacity
            );

            var secondClip = new VideoClip(
                filePath,
                splitFrame,
                (long)(timelineStart + splitFrame * (timeline.Fps / source.FPS)),
                new SourceMedia(source.FilePath),
                length: length - splitFrame,
                x,
                y,
                scale,
                opacity
            );

            return (firstClip, secondClip);
        }


    }
}
