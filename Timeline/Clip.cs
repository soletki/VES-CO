using System.IO;
using System.Windows.Controls;

namespace VESCO.Timeline
{
    public abstract class Clip
    {
        public string Name { get; set; }
        public string FilePath { get; set; }
        public long TimelineStart { get; set; }
        public Border? Rect { get; set; }

        protected Clip(string filePath, long timelineStart)
        {
            FilePath = filePath;
            Name = Path.GetFileName(filePath);
            TimelineStart = timelineStart;
        }
    }
}
