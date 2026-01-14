using OpenCvSharp;

namespace VESCO.Timeline
{
    public class VideoSource : SourceFile
    {
        public long FrameCount { get; set; }
        public double FPS { get; set; }

        public VideoSource(string filePath) : base(filePath)
        {
            using var cap = new VideoCapture(filePath);

            FrameCount = cap.FrameCount;
            FPS = cap.Fps;
        }
    }
}
