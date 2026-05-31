using System.Diagnostics;
using System.Windows.Media.Imaging;

namespace VESCO.Timeline
{
    public class VideoTrack : Track
    {
        public double Fps { get; }
        public List<VideoClip> Clips { get; set; } = new();

        public VideoTrack(string name, double fps) : base(name)
        {
            Fps = fps;
        }

        public void AddClip(VideoClip clip)
        {
            if (clip == null)
                return;

            Clips.Add(clip);
        }

        public void RemoveClip(VideoClip clip)
        { 
            Clips.Remove(clip);
        }

        public BitmapSource GetFrameAt(long timelineFrame, double scale)
        {
            foreach (var clip in Clips)
            {
                long clipEndFrame = clip.TimelineStart + (long)(clip.Length * (Fps / clip.Source.FPS));

                if (timelineFrame >= clip.TimelineStart && timelineFrame < clipEndFrame)
                {
                    return clip.GetFrameAtTimelineFrame(timelineFrame, scale);
                }
            }

            return null;
        }

        public VideoClip? GetClipAt(long timelineFrame)
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

        public VideoClip? getClipAt(long timelineFrame)
        {
            return GetClipAt(timelineFrame);
        }
    }
}
