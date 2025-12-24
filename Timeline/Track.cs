namespace VESCO.Timeline
{
    public abstract class Track<TClip> where TClip : Clip
    {
        public string Name { get; set; }
        public List<TClip> Clips { get; set; } = new();

        protected Track(string name)
        {
            Name = name;
        }

        public void AddClip(TClip clip)
        {
            double newClipStart = clip.TimelineStart;
            double newClipEnd = clip.TimelineStart + clip.Source.Length;

            foreach (var existingClip in Clips)
            {
                double existingClipStart = existingClip.TimelineStart;
                double existingClipEnd = existingClip.TimelineStart + existingClip.Source.Length;

                if (newClipStart < existingClipEnd && newClipEnd > existingClipStart)
                {
                    throw new InvalidOperationException($"Clip '{clip.Name}' overlaps with existing clip '{existingClip.Name}' on track '{Name}'.");
                }
            }

            Clips.Add(clip);
        }
        public void RemoveClip(TClip clip) => Clips.Remove(clip);
    }
}
