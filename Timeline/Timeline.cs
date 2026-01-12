using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using System.Windows.Media.Imaging;

namespace VESCO.Timeline
{
    public class Timeline
    {
        public double Fps { get; set; }
        public List<VideoTrack> VideoTracks { get; set; } = new();

        public double PreviewScale { get; set; } = 0.5;

        public Timeline(double fps)
        {
            Fps = fps;
        }

        public long GetTotalFrames()
        {
            long maxFrame = 0;

            foreach (var track in VideoTracks)
            {
                foreach (var clip in track.Clips)
                {
                    long clipEnd = clip.TimelineStart + (long)(clip.Length * (Fps / clip.Source.FPS));
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
                    frames.Add(new FrameWrapper(trackFrame, clip.opacity, clip.x, clip.y, clip.scale));
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

                        int dstX = frame.x;
                        int dstY = frame.y;

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
                            srcROI.CopyTo(dstROI);
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
    }
}