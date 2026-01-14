using System.IO;
using System.Windows.Controls;

namespace VESCO.Timeline
{
    public abstract class Clip(string filePath, long timelineStart)
    {
        public string name { get; set; } = Path.GetFileName(filePath);
        public string filePath { get; set; } = filePath;
        public long timelineStart { get; set; } = timelineStart;
        public Border rect { get; set; }
    }
}
