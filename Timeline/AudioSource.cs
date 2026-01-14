using NAudio.Wave;

namespace VESCO.Timeline
{
    public class AudioSource : SourceFile
    {
        public double Duration { get; set; }
        public int SampleRate { get; set; }
        public int Channels { get; set; }

        public AudioSource(string filePath) : base(filePath)
        {

            using var reader = new AudioFileReader(filePath);
            Duration = reader.TotalTime.TotalSeconds;
            SampleRate = reader.WaveFormat.SampleRate;
            Channels = reader.WaveFormat.Channels;
        }
    }
}
