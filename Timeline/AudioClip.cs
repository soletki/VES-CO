namespace VESCO.Timeline
{
    public class AudioClip : Clip
    {
        public AudioClip(string name, double srcStart, double timelineStart, SourceMedia source)
            : base(name, srcStart, timelineStart, source) { }
    }
}
