using OpenCvSharp;

namespace VESCO.Timeline
{
    public class VideoSource : SourceFile
    {
        public long FrameCount { get; set; }
        public double FPS { get; set; }
        public int Width { get; }
        public int Height { get; }

        public VideoSource(string filePath) : base(filePath)
        {
            using var cap = new VideoCapture(filePath);

            FrameCount = cap.FrameCount;
            FPS = cap.Fps;
            Width = cap.FrameWidth;
            Height = cap.FrameHeight;
        }
    }
}
