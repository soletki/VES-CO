
using System.IO;
using System.Windows.Media.Imaging;
using Xabe.FFmpeg;

namespace VESCO.Timeline
{
    public class Timeline
    {
        public double fps { get; set; }
        public List<VideoTrack> VideoTracks { get; set; } = new();
        public List<AudioTrack> AudioTracks { get; set; } = new();

        public Timeline(double fps)
        {
            this.fps = fps;
            VideoTracks.Add(new VideoTrack("V1"));
            AudioTracks.Add(new AudioTrack("A1"));
        }

        public async Task<BitmapImage> getFrameAt(double seconds)
        {
            return await VideoTracks[0].getFrameAt(seconds, fps);
        }

        public double getTotalDuration()
        {
            double maxDuration = 0;
            foreach (var track in VideoTracks)
            {
                foreach (var clip in track.Clips)
                {
                    double clipEnd = clip.TimelineStart + clip.Source.Length;
                    if (clipEnd > maxDuration)
                    {
                        maxDuration = clipEnd;
                    }
                }
            }
            return maxDuration;
        }
    }

    

    
}
