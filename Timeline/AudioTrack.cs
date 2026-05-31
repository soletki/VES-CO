using NAudio.Wave;

namespace VESCO.Timeline
{
    public class AudioTrack : Track
    {
        public List<AudioClip> Clips { get; set; } = new();
        public double Volume { get; set; } = 1.0;
        public bool IsMuted { get; set; } = false;

        public AudioTrack(string name) : base(name)
        {
        }

        public void AddClip(AudioClip clip)
        {
            Clips.Add(clip);
        }

        public void RemoveClip(AudioClip clip)
        {
            Clips.Remove(clip);
        }

        public float[]? GetAudioAtFrame(long frame, double fps, int sampleRate, int channels)
        {
            if (IsMuted) return null;

            foreach (var clip in Clips)
            {
                long clipEndFrame = clip.TimelineStart + (long)(clip.Duration * fps);

                if (frame >= clip.TimelineStart && frame < clipEndFrame)
                {
                    double frameTime = frame / fps;
                    double sourceTime = frameTime - (clip.TimelineStart / fps) + clip.SourceStart;

                    using var reader = new AudioFileReader(clip.Source.FilePath);
                    reader.CurrentTime = TimeSpan.FromSeconds(sourceTime);

                    int samplesPerFrame = (int)(sampleRate / fps * channels);
                    float[] buffer = new float[samplesPerFrame];
                    reader.Read(buffer, 0, samplesPerFrame);

                    for (int i = 0; i < buffer.Length; i++)
                    {
                        buffer[i] *= (float)(Volume * clip.Volume);
                    }

                    return buffer;
                }
            }

            return null;
        }
    }
}
