using NAudio.Wave;
using System.IO;

namespace VESCO.Timeline
{
    public class AudioSource
    {
        public string FilePath { get; set; }
        public string FileName { get; set; }
        public double Duration { get; set; }
        public int SampleRate { get; set; }
        public int Channels { get; set; }

        public AudioSource(string filePath)
        {
            FilePath = filePath;
            FileName = Path.GetFileName(filePath);

            using var reader = new AudioFileReader(filePath);
            Duration = reader.TotalTime.TotalSeconds;
            SampleRate = reader.WaveFormat.SampleRate;
            Channels = reader.WaveFormat.Channels;
        }
    }
}
