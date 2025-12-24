namespace VESCO.Timeline
{
    public abstract class Clip
    {
        public string Name { get; set; }

        public double SourceStart { get; set; }

        public double TimelineStart { get; set; }

        public SourceMedia Source { get; set; }

        protected Clip(string name, double sourceStart, double timelineStart, SourceMedia source)
        {
            Name = name;
            SourceStart = sourceStart;
            TimelineStart = timelineStart;
            Source = source;
        }
    }
}
