using NAudio.Wave;
using VESCO.Timeline;

namespace VESCO.Timeline
{
    public class AudioClip : Clip
    {
        public AudioSource Source { get; }
        public double SourceStart { get; }
        public double Duration { get; set; }
        public double Volume { get; set; } = 1.0;

        public AudioClip(string filePath, double sourceStart, long timelineStart, AudioSource source)
            : base(filePath, timelineStart)
        {
            Source = source;
            SourceStart = sourceStart;
            Duration = source.Duration - sourceStart;
        }

        public float[]? GetAudioAtTimelineFrame(long timelineFrame, double timelineFps, int sampleRate, int channels)
        {
            long relativeFrame = timelineFrame - TimelineStart;
            double timeOffset = relativeFrame / timelineFps;
            double sourceTime = SourceStart + timeOffset;

            if (sourceTime < SourceStart || sourceTime >= SourceStart + Duration)
                return null;

            using var reader = new AudioFileReader(Source.FilePath);
            reader.CurrentTime = TimeSpan.FromSeconds(sourceTime);

            int samplesPerFrame = (int)(sampleRate / timelineFps * channels);
            float[] buffer = new float[samplesPerFrame];
            int samplesRead = reader.Read(buffer, 0, samplesPerFrame);

            for (int i = 0; i < samplesRead; i++)
            {
                buffer[i] *= (float)Volume;
            }

            if (samplesRead < samplesPerFrame)
            {
                Array.Resize(ref buffer, samplesRead);
            }

            return buffer;
        }

        public (AudioClip?, AudioClip?) SplitAtTime(double splitTime, Timeline timeline)
        {
            if (splitTime <= 0 || splitTime >= Duration)
                return (null, null);

            var firstPart = new AudioClip(
                FilePath,
                SourceStart,
                TimelineStart,
                Source);
            firstPart.Duration = splitTime;

            var secondPart = new AudioClip(
                FilePath,
                splitTime + SourceStart,
                TimelineStart + (long)((splitTime) * timeline.Fps),
                Source);
            secondPart.Duration = Duration - firstPart.Duration;

            return (firstPart, secondPart);
        }
    }
}
