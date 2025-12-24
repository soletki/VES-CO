using System.Diagnostics;
using System.Windows.Media.Imaging;

namespace VESCO.Timeline
{
    public class VideoTrack : Track<VideoClip>
    {
        public VideoTrack(string name) : base(name) { }

        public async Task<BitmapImage> getFrameAt(
            double timelineSeconds,
            double timelineFps
        )
        {
            foreach (var clip in Clips)
            {
                double clipStart = clip.TimelineStart;
                double clipEnd = clip.TimelineStart + clip.Source.Length;

                if (timelineSeconds >= clipStart &&
                    timelineSeconds < clipEnd)
                {
                    var frame = await clip.getFrameAt(timelineSeconds, timelineFps);
                    if (frame != null)
                        Debug.WriteLine($"[Timeline] Retrieved frame from clip '{clip.Name}' at {timelineSeconds:F2}s");
                    return frame;

                }
            }

            Debug.WriteLine($"[Timeline] No clip found at {timelineSeconds:F2}s on track '{Name}'");
            return null;
        }

    }
}
