using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
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

        public BitmapSource GetFrameAtTimelineFrame(long timelineFrame, Timeline timeline)
        {
            double fpsRatio = Source.FPS / timeline.Fps;

            long localFrame =
                SourceStart +
                (long)Math.Round((timelineFrame - TimelineStart) * fpsRatio);

            localFrame = Math.Clamp(localFrame, 0, Source.FrameCount - 1);

            _capture.Set(VideoCaptureProperties.PosFrames, localFrame);

            using var mat = new Mat();
            if (!_capture.Read(mat) || mat.Empty())
                return null;

            Cv2.Resize(mat, mat, new OpenCvSharp.Size(), 1, 1, InterpolationFlags.Nearest);

            return mat.ToBitmapSource();
        }

    }
}
