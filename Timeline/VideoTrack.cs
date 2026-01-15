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
            long newClipEnd = (long)(clip.TimelineStart + clip.Length * (Fps / clip.Source.FPS));

            foreach (var existingClip in Clips)
            {
                long existingClipStart = existingClip.TimelineStart;
                long existingClipEnd = (long)(existingClip.TimelineStart + existingClip.Length * (Fps / existingClip.Source.FPS));
            }

            Clips.Add(clip);
        }

        public void RemoveClip(VideoClip clip) => Clips.Remove(clip);

        public BitmapSource GetFrameAt(long timelineFrame, double scale)
        {
            foreach (var clip in Clips)
            {
                long clipEndFrame = clip.TimelineStart + (long)(clip.Length * (Fps / clip.Source.FPS));

                if (timelineFrame >= clip.TimelineStart && timelineFrame < clipEndFrame)
                {
                    return clip.GetFrameAtTimelineFrame(timelineFrame, Fps, scale);
                }
            }

            return null;
        }

        public VideoClip getClipAt(long timelineFrame)
        {
            foreach (var clip in Clips)
            {
                long clipEndFrame = clip.TimelineStart + (long)(clip.Length * (Fps / clip.Source.FPS));
                if (timelineFrame >= clip.TimelineStart && timelineFrame < clipEndFrame)
                {
                    return clip;
                }
            }
            return null;
        }
    }
}