using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using System.Windows.Media.Imaging;

namespace VESCO.Timeline
{
    public class VideoClip : Clip
    {
        private VideoCapture _capture;

        public VideoClip(string name, long sourceStartFrame, long timelineStartFrame, SourceMedia source)
            : base(name, sourceStartFrame, timelineStartFrame, source)
        {
            _capture = new VideoCapture(source.FilePath);
        }

        public BitmapSource GetFrameAtTimelineFrame(long timelineFrame)
        {
            long localFrame =
                timelineFrame - TimelineStart + SourceStart;

            localFrame = Math.Clamp(localFrame, 0, Source.FrameCount - 1);

            _capture.Set(VideoCaptureProperties.PosFrames, localFrame);

            using var mat = new Mat();
            if (!_capture.Read(mat) || mat.Empty())
                return null;

            return mat.ToBitmapSource();
        }
    }
}
