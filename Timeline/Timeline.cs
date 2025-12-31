using System.Windows.Media.Imaging;

namespace VESCO.Timeline
{
    public class Timeline
    {
        public double Fps { get; }
        public List<VideoTrack> VideoTracks { get; } = new();

        public Timeline(double fps)
        {
            Fps = fps;
            VideoTracks.Add(new VideoTrack("V1", Fps));
        }

        public BitmapSource GetFrameAtTime(double seconds)
        {
            long frame = (long)Math.Round(seconds * Fps);
            return GetFrameAtFrame(frame);
        }

        public BitmapSource GetFrameAtFrame(long frame)
        {
            return VideoTracks[0].GetFrameAt(frame);
        }

        public long GetTotalFrames()
        {
            long max = 0;

            foreach (var clip in VideoTracks[0].Clips)
            {
                long end = (long)(clip.TimelineStart + clip.Length * (Fps/clip.Source.FPS));

                if (end > max)
                    max = end;
            }

            return max;
        }


        public double GetTotalDuration()
            => GetTotalFrames() / Fps;
    }
}
