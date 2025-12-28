namespace VESCO.Timeline
{
    public abstract class Clip
    {
        public string Name { get; set; }

        public long SourceStart { get; set; }

        public long TimelineStart { get; set; }

        public SourceMedia Source { get; set; }

        protected Clip(string name, long sourceStart, long timelineStart, SourceMedia source)
        {
            Name = name;
            SourceStart = sourceStart;
            TimelineStart = timelineStart;
            Source = source;
        }
    }
}
