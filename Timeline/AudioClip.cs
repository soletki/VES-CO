using NAudio.Wave;
using System.Windows.Controls;
using VESCO.Timeline;

public class AudioClip : Clip
{
    public AudioSource Source { get; set; }
    public double SourceStartTime { get; set; }
    public double Duration { get; set; }
    public double Volume { get; set; } = 1.0;

    public AudioClip(string filePath, double sourceStartTime, long timelineStartFrame, AudioSource source)
        : base(filePath, timelineStartFrame)
    {
        Source = source;
        SourceStartTime = sourceStartTime;
        Duration = source.Duration - sourceStartTime;
    }

    public float[] GetAudioAtTimelineFrame(long timelineFrame, double timelineFps, int sampleRate, int channels)
    {
        long relativeFrame = timelineFrame - timelineStart;
        double timeOffset = relativeFrame / timelineFps;
        double sourceTime = SourceStartTime + timeOffset;

        if (sourceTime < SourceStartTime || sourceTime >= SourceStartTime + Duration)
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

    public (AudioClip?, AudioClip?) SplitAtTime(double splitTime)
    {
        if (splitTime <= SourceStartTime || splitTime >= SourceStartTime + Duration)
            return (null, null);

        var firstPart = new AudioClip(
            filePath,
            SourceStartTime,
            timelineStart,
            Source);
        firstPart.Duration = splitTime - SourceStartTime;

        var secondPart = new AudioClip(
            filePath,
            splitTime,
            timelineStart, // This needs proper calculation
            Source);
        secondPart.Duration = Duration - firstPart.Duration;

        return (firstPart, secondPart);
    }
}
