using System.Windows.Media.Imaging;

namespace VESCO.Timeline
{
    public class VideoTrack : Track<VideoClip>
    {
        public VideoTrack(string name) : base(name) { }

        public BitmapSource GetFrameAt(long timelineFrame, Timeline timeline)
        {
            foreach (var clip in Clips)
            {
                if (timelineFrame >= clip.TimelineStart &&
                    timelineFrame < clip.TimelineStart + clip.Length*(timeline.Fps/clip.Source.FPS))
                {
                    return clip.GetFrameAtTimelineFrame(timelineFrame, timeline);
                }
            }

            return null;
        }
    }
}
