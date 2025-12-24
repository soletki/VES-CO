namespace VESCO.Timeline
{
    public class SourceMedia
    {
        public string FilePath { get; set; }
        public long FrameCount { get; set; }
        public double Length { get; set; }

        public SourceMedia(string filePath, long frameCount, double length)
        {
            FilePath = filePath;
            FrameCount = frameCount;
            Length = length;
        }
    }
}
