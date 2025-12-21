using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using Xabe.FFmpeg;

namespace VESCO
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
            return await VideoTracks[0].getFrameAt(seconds);
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

    public abstract class Track<TClip> where TClip : Clip
    {
        public string Name { get; set; }
        public List<TClip> Clips { get; set; } = new();

        protected Track(string name)
        {
            Name = name;
        }

        public void AddClip(TClip clip)
        {
            double newClipStart = clip.TimelineStart;
            double newClipEnd = clip.TimelineStart + clip.Source.Length;
            
            foreach (var existingClip in Clips)
            {
                double existingClipStart = existingClip.TimelineStart;
                double existingClipEnd = existingClip.TimelineStart + existingClip.Source.Length;

                // Check if there's an overlap
                if (newClipStart < existingClipEnd && newClipEnd > existingClipStart)
                {
                    throw new InvalidOperationException($"Clip '{clip.Name}' overlaps with existing clip '{existingClip.Name}' on track '{Name}'.");
                }
            }

            Clips.Add(clip);
        }
        public void RemoveClip(TClip clip) => Clips.Remove(clip);
    }

    public class VideoTrack : Track<VideoClip>
    {
        public VideoTrack(string name) : base(name) { }

        public async Task<BitmapImage> getFrameAt(double seconds)
        {
            for (int i = 0; i < Clips.Count; i++)
            {
                if (Clips[i].TimelineStart <= seconds && Clips[i].TimelineStart + Clips[i].Source.Length > seconds)
                {
                    double srcTime = seconds - Clips[i].TimelineStart + Clips[i].SourceStart;

                    return await Clips[i].getFrameAt(srcTime);
                }   
            }

            return null;
        }
    }

    public class AudioTrack : Track<AudioClip>
    {
        public AudioTrack(string name) : base(name) { }
    }

    public class SourceMedia
    {
        public string FilePath { get; set; }
        public long FrameCount { get; set; }
        public double Length { get; set; }

        public SourceMedia(string filePath, long frameCount, double length)
        {
            FilePath = filePath;
            FrameCount = frameCount;
            Length = length;
        }
    }

    public abstract class Clip
    {
        public string Name { get; set; }

        public double SourceStart { get; set; }

        public double TimelineStart { get; set; }

        public SourceMedia Source { get; set; }

        protected Clip(string name, double sourceStart, double timelineStart, SourceMedia source)
        {
            Name = name;
            SourceStart = sourceStart;
            TimelineStart = timelineStart;
            Source = source;
        }
    }

    public class VideoClip : Clip
    {
        public VideoClip(string name, double srcStart, double timelineStart, SourceMedia source)
            : base(name, srcStart, timelineStart, source) { }

        public async Task<BitmapImage> getFrameAt(double seconds)
        {
            if (string.IsNullOrEmpty(Source.FilePath))
                return null;

            string tempFrame = Path.Combine(Path.GetTempPath(), $"frame_{Guid.NewGuid()}.jpg");

            double srcTime = seconds + SourceStart;

            var conversion = Xabe.FFmpeg.FFmpeg.Conversions.New()
                .AddParameter($"-ss {srcTime.ToString(System.Globalization.CultureInfo.InvariantCulture)}")
                .AddParameter($"-i \"{Source.FilePath}\"")
                .AddParameter("-vf scale=720:-1")
                .AddParameter("-vframes 1")
                .AddParameter($"\"{tempFrame}\"", ParameterPosition.PostInput)
                .AddParameter("-q:v 3");


            await conversion.Start();

            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.UriSource = new Uri(tempFrame);
            try
            {
                bmp.EndInit();
            }
            catch
            {
                return null;
            }
            


            try { File.Delete(tempFrame); } catch { }

            return bmp;
        }
    }

    public class AudioClip : Clip
    {
        public AudioClip(string name, double srcStart, double timelineStart, SourceMedia source)
            : base(name, srcStart, timelineStart, source) { }
    }
}
