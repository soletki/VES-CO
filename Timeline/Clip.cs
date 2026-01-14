using System.IO;
using System.Windows.Controls;

namespace VESCO.Timeline
{
    public abstract class Clip
    {
        public string name { get; set; }
        public string filePath { get; set; }
        public long timelineStart { get; set; }
        public Border rect { get; set; }

        protected Clip(string filePath, long timelineStart)
        {
            this.filePath = filePath;
            this.timelineStart = timelineStart;
            name = Path.GetFileName(filePath);
        }
    }
}
