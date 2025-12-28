using System.Windows.Media.Imaging;

namespace VESCO.Timeline
{
    public class VideoTrack : Track<VideoClip>
    {
        public VideoTrack(string name) : base(name) { }

        public BitmapSource GetFrameAt(long timelineFrame)
        {
            foreach (var clip in Clips)
            {
                if (timelineFrame >= clip.TimelineStart &&
                    timelineFrame < clip.TimelineStart + clip.Source.FrameCount)
                {
                    return clip.GetFrameAtTimelineFrame(timelineFrame);
                }
            }

            return null;
        }
    }
}
