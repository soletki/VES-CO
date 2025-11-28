using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VESCO
{
    public class Timeline
    {
        private double fps { get; set; }
        public List<VideoTrack> VideoTracks { get; set; } = new();
        public List<AudioTrack> AudioTracks { get; set; } = new();

        public Timeline(double fps)
        {
            this.fps = fps;
        }
    }

    public abstract class Track<TClip> where TClip : Clip
    {
        public string Name { get; set; }
        public List<TClip> Clips { get; set; } = new();

        protected Track(string name)
        {
            Name = name;
        }

        public void AddClip(TClip clip) => Clips.Add(clip);
        public void RemoveClip(TClip clip) => Clips.Remove(clip);
    }

    public class VideoTrack : Track<VideoClip>
    {
        public VideoTrack(string name) : base(name) { }
    }

    public class AudioTrack : Track<AudioClip>
    {
        public AudioTrack(string name) : base(name) { }
    }

    public class SourceMedia
    {
        public string FilePath { get; set; }
        public long FrameCount { get; set; }

        public SourceMedia(string filePath, long frameCount)
        {
            FilePath = filePath;
            FrameCount = frameCount;
        }
    }

    public abstract class Clip
    {
        public string Name { get; set; }

        public long SourceStartFrame { get; set; }

        public long TimelineStartFrame { get; set; }

        public SourceMedia Source { get; set; }

        protected Clip(string name, long sourceStartFrame, long timelineStartFrame, SourceMedia source)
        {
            Name = name;
            SourceStartFrame = sourceStartFrame;
            TimelineStartFrame = timelineStartFrame;
            Source = source;
        }
    }

    public class VideoClip : Clip
    {
        public VideoClip(string name, long srcStart, long timelineStart, SourceMedia source)
            : base(name, srcStart, timelineStart, source) { }
    }

    public class AudioClip : Clip
    {
        public AudioClip(string name, long srcStart, long timelineStart, SourceMedia source)
            : base(name, srcStart, timelineStart, source) { }
    }
}
