using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using System.Windows.Media.Imaging;

namespace VESCO.Timeline
{
    public class VideoClip : Clip
    {
        private VideoCapture _capture;
        private long _lastFrame = -1;

        public int X { get; set; }
        public int Y { get; set; }
        public double Scale { get; set; } = 1.0;
        public double Opacity { get; set; } = 1.0;

        public VideoSource Source { get; }
        public long SourceStart { get; }
        public long Length { get; }

        public VideoClip(string filePath, long sourceStart, long timelineStart, VideoSource source, long length = -1, int x = 0, int y = 0, double scale = 1.0, double opacity = 1.0)
            : base(filePath, timelineStart)
        {
            _capture = new VideoCapture(source.FilePath);
            Source = source;
            SourceStart = sourceStart;
            X = x;
            Y = y;
            Scale = scale;
            Opacity = opacity;
            Length = length != -1 ? length : Source.FrameCount - SourceStart;
        }

        public BitmapSource GetFrameAtTimelineFrame(long timelineFrame, double fps, double scale)
        {
            double fpsRatio = Source.FPS / fps;
            long localFrame = SourceStart +
                (long)Math.Round((timelineFrame - TimelineStart) * fpsRatio);

            localFrame = Math.Clamp(localFrame, 0, Source.FrameCount - 1);

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
            if (splitFrame <= 0 || splitFrame >= Length)
                return (this, null);

            var firstClip = new VideoClip(
                FilePath,
                SourceStart,
                TimelineStart,
                new VideoSource(Source.FilePath),
                length: splitFrame,
                X,
                Y,
                Scale,
                Opacity
            );

            var secondClip = new VideoClip(
                FilePath,
                splitFrame,
                (long)(TimelineStart + splitFrame * (timeline.Fps / Source.FPS)),
                new VideoSource(Source.FilePath),
                length: Length - splitFrame,
                X,
                Y,
                Scale,
                Opacity
            );

            return (firstClip, secondClip);
        }


    }
}
