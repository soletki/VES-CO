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

            long newClipStart = clip.timelineStart;
            long newClipEnd = (long)(clip.timelineStart + clip.length * (Fps / clip.source.FPS));

            foreach (var existingClip in Clips)
            {
                long existingClipStart = existingClip.timelineStart;
                long existingClipEnd = (long)(existingClip.timelineStart + existingClip.length * (Fps/existingClip.source.FPS));
            }

            Clips.Add(clip);
        }

        public void RemoveClip(VideoClip clip) => Clips.Remove(clip);

        public BitmapSource GetFrameAt(long timelineFrame, double scale)
        {
            foreach (var clip in Clips)
            {
                long clipEndFrame = clip.timelineStart + (long)(clip.length * (Fps / clip.source.FPS));

                if (timelineFrame >= clip.timelineStart && timelineFrame < clipEndFrame)
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
                long clipEndFrame = clip.timelineStart + (long)(clip.length * (Fps / clip.source.FPS));
                if (timelineFrame >= clip.timelineStart && timelineFrame < clipEndFrame)
                {
                    return clip;
                }
            }
            return null;
        }
    }
}