using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace VESCO.Timeline
{
    public class VideoClip : Clip
    {
        private VideoCapture _capture;

        public VideoClip(string name, long sourceStartFrame, long timelineStartFrame, SourceMedia source, long ?length = null)
            : base(name, sourceStartFrame, timelineStartFrame, source, length)
        {
            _capture = new VideoCapture(source.FilePath);
        }

        private long _lastFrame = -1;

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

            Cv2.Resize(mat, mat, new OpenCvSharp.Size(mat.Width * scale, mat.Height * scale), 0, 0, InterpolationFlags.Area);

            return mat.ToBitmapSource();
        }


        public (VideoClip first, VideoClip second) SplitAtFrame(long splitFrame, Timeline timeline)
        {
            if (splitFrame <= 0 || splitFrame >= this.Length)
                return (this, null);

            var firstClip = new VideoClip(
                Name,
                SourceStart,
                TimelineStart,
                new SourceMedia(Source.FilePath),
                length: splitFrame
            );

            var secondClip = new VideoClip(
                Name,
                splitFrame,
                (long)(TimelineStart + splitFrame * (timeline.Fps / Source.FPS)),
                new SourceMedia(Source.FilePath),
                length: Length - splitFrame
            );

            return (firstClip, secondClip);
        }


    }
}
