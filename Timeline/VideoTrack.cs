using System.Diagnostics;
using System.Windows.Media.Imaging;

namespace VESCO.Timeline
{
    public class VideoTrack
    {
        public string Name { get; set; }
        public double Fps { get; }
        public List<VideoClip> Clips { get; set; } = new();

        public VideoTrack(string name, double fps)
        {
            Name = name;
            Fps = fps;
        }

        public void AddClip(VideoClip clip)
        {
            if (clip == null)
                return;

            long newClipStart = clip.TimelineStart;
            long newClipEnd = clip.TimelineStart + clip.Source.FrameCount;

            foreach (var existingClip in Clips)
            {
                long existingClipStart = existingClip.TimelineStart;
                long existingClipEnd = (long)(existingClip.TimelineStart + existingClip.Length * (Fps/existingClip.Source.FPS));

                if (newClipStart < existingClipEnd && newClipEnd > existingClipStart)
                {
                    Debug.WriteLine($"Failed to add clip '{clip.Name}': overlaps with existing clip '{existingClip.Name}'.");
                    Debug.WriteLine($"New Clip: Start={newClipStart}, End={newClipEnd}");
                    Debug.WriteLine($"Existing Clip: Start={existingClipStart}, End={existingClipEnd}");
                    return; // Don't add overlapping clips
                }
            }

            Clips.Add(clip);
        }

        public void RemoveClip(VideoClip clip) => Clips.Remove(clip);

        public BitmapSource GetFrameAt(long timelineFrame)
        {
            foreach (var clip in Clips)
            {
                long clipEndFrame = clip.TimelineStart + (long)(clip.Length * (Fps / clip.Source.FPS));

                if (timelineFrame >= clip.TimelineStart && timelineFrame < clipEndFrame)
                {
                    return clip.GetFrameAtTimelineFrame(timelineFrame, Fps);
                }
            }

            return null;
        }
    }
}