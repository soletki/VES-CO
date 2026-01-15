using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using System.Windows.Media.Imaging;

namespace VESCO.Timeline
{
    public class Timeline
    {
        public double Fps { get; set; }
        public List<VideoTrack> VideoTracks { get; set; } = new();
        public List<AudioTrack> AudioTracks { get; set; } = new();
        public double PreviewScale { get; set; } = 0.5;
        public Timeline(double fps)
        {
            Fps = fps;
        }

        public long GetTotalFrames()
        {
            long maxFrame = 0;

            foreach (VideoTrack track in VideoTracks)
            {
                foreach (VideoClip clip in track.Clips)
                {
                    long clipEnd = clip.TimelineStart + (long)(clip.Length * (Fps / clip.Source.FPS));
                    if (clipEnd > maxFrame)
                        maxFrame = clipEnd;
                }
            }

            foreach (AudioTrack track in AudioTracks)
            {
                foreach (AudioClip clip in track.Clips)
                {
                    long clipEnd = clip.TimelineStart + (long)(clip.Duration * Fps);
                    if (clipEnd > maxFrame)
                        maxFrame = clipEnd;
                }
            }
            return maxFrame;
        }

        public BitmapSource GetFrameAtFrame(long frame)
        {
            return CompositeFrames(frame);
        }

        private BitmapSource CompositeFrames(long frame)
        {
            List<FrameWrapper> frames = new List<FrameWrapper>();

            foreach (var track in VideoTracks)
            {
                BitmapSource trackFrame = track.GetFrameAt(frame, PreviewScale);
                VideoClip clip = track.getClipAt(frame);

                if (trackFrame != null && clip!=null)
                {
                    frames.Add(new FrameWrapper(trackFrame, clip.Opacity, clip.X, clip.Y, clip.Scale));
                }
            }

            if (frames.Count == 0)
                return null;

            return CompositeImages(frames);
        }

        private BitmapSource CompositeImages(List<FrameWrapper> frames)
        {
            if (frames.Count == 0)
                return null;

            int maxWidth = frames.Max(f => f.frame.PixelWidth);
            int maxHeight = frames.Max(f => f.frame.PixelHeight);

            Mat result = null;

            try
            {
                result = new Mat(maxHeight, maxWidth, MatType.CV_8UC3, new Scalar(0, 0, 0));

                frames.Reverse();

                foreach (var frame in frames)
                {
                    using (Mat frameMat = BitmapSourceConverter.ToMat(frame.frame))
                    {
                        Mat frameToLayer = frameMat;
                        if (frameMat.Channels() == 1)
                        {
                            frameToLayer = new Mat();
                            Cv2.CvtColor(frameMat, frameToLayer, ColorConversionCodes.GRAY2BGR);
                        }
                        else if (frameMat.Channels() == 4)
                        {
                            frameToLayer = new Mat();
                            Cv2.CvtColor(frameMat, frameToLayer, ColorConversionCodes.BGRA2BGR);
                        }

                        if(frame.scale != 1.0)
                        {
                            if(frame.scale <= 0.0)
                            {
                                if (frameToLayer != frameMat)
                                    frameToLayer.Dispose();
                                continue;
                            }

                            Mat resized = new Mat();
                            Cv2.Resize(frameToLayer, resized, new Size(frameToLayer.Width * frame.scale, frameToLayer.Height * frame.scale), 0, 0, InterpolationFlags.Area);
                            if (frameToLayer != frameMat)
                                frameToLayer.Dispose();
                            frameToLayer = resized;
                        }

                        int dstX = (int)(frame.x * PreviewScale);
                        int dstY = (int)(frame.y * PreviewScale);

                        int srcX = 0;
                        int srcY = 0;

                        int width = frameToLayer.Width;
                        int height = frameToLayer.Height;

                        // Clip left/top
                        if (dstX < 0)
                        {
                            srcX = -dstX;
                            width += dstX;
                            dstX = 0;
                        }
                        if (dstY < 0)
                        {
                            srcY = -dstY;
                            height += dstY;
                            dstY = 0;
                        }

                        // Clip right/bottom
                        if (dstX + width > result.Width)
                            width = result.Width - dstX;

                        if (dstY + height > result.Height)
                            height = result.Height - dstY;

                        // Completely outside
                        if (width <= 0 || height <= 0)
                        {
                            if (frameToLayer != frameMat)
                                frameToLayer.Dispose();
                            continue;
                        }

                        Rect srcRect = new Rect(srcX, srcY, width, height);
                        Rect dstRect = new Rect(dstX, dstY, width, height);

                        using (Mat srcROI = new Mat(frameToLayer, srcRect))
                        using (Mat dstROI = new Mat(result, dstRect))
                        {
                            double alpha = Math.Clamp(frame.opacity, 0.0, 1.0);
                            if(alpha>=0.999)
                                srcROI.CopyTo(dstROI);
                            else if (alpha > 0.001)
                                Cv2.AddWeighted(srcROI, alpha, dstROI, 1.0 - alpha, 0.0, dstROI);
                        }

                        if (frameToLayer != frameMat)
                            frameToLayer.Dispose();
                    }
                }

                BitmapSource output = BitmapSourceConverter.ToBitmapSource(result);
                output.Freeze();
                return output;
            }
            finally
            {
                result?.Dispose();
            }
        }

        public float[]? GetAudioAtFrame(long frame, int sampleRate = 48000, int channels = 2)
        {
            List<float[]> trackAudios = new List<float[]>();

            foreach (var track in AudioTracks)
            {
                var audio = track.GetAudioAtFrame(frame, Fps, sampleRate, channels);
                if (audio != null)
                {
                    trackAudios.Add(audio);
                }
            }

            if (trackAudios.Count == 0) return null;
            if (trackAudios.Count == 1) return trackAudios[0];

            return MixAudio(trackAudios);
        }

        private float[] MixAudio(List<float[]> tracks)
        {
            int maxLength = tracks.Max(t => t.Length);
            float[] mixed = new float[maxLength];

            foreach (var track in tracks)
            {
                for (int i = 0; i < track.Length; i++)
                {
                    mixed[i] += track[i];
                }
            }

            float max = mixed.Max(Math.Abs);
            if (max > 1.0f)
            {
                for (int i = 0; i < mixed.Length; i++)
                {
                    mixed[i] /= max;
                }
            }

            return mixed;
        }
    }
}