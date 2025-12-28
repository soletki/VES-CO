using OpenCvSharp;

namespace VESCO.Timeline
{
    public class SourceMedia
    {
        public string FilePath { get; set; }
        public long FrameCount { get; set; }
        public double FPS { get; set; }

        public SourceMedia(string filePath)
        {
            FilePath = filePath;

            using var cap = new VideoCapture(filePath);

            FrameCount = cap.FrameCount;
            FPS = cap.Fps;
        }
    }
}
