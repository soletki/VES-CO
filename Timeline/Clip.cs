using System.Diagnostics;
using System.Windows.Controls;

namespace VESCO.Timeline
{
    public abstract class Clip
    {
        public string Name { get; set; }

        public long Length { get; set; }

        public long SourceStart { get; set; }

        public long TimelineStart { get; set; }

        public SourceMedia Source { get; set; }

        public Border rect { get; set; }

        protected Clip(
            string name,
            long sourceStart,
            long timelineStart,
            SourceMedia source,
            long? length = null)
        {
            Name = name;
            SourceStart = sourceStart;
            TimelineStart = timelineStart;
            Source = source;

            Length = length ?? (source.FrameCount - SourceStart);

            Debug.WriteLine($"New clip with Length: {Length} Starting at frame: {TimelineStart}");
        }

    }
}
