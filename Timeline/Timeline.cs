using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using System.Windows.Media.Imaging;

namespace VESCO.Timeline
{
    public class Timeline
    {
        public double Fps { get; set; }
        public List<VideoTrack> VideoTracks { get; set; } = new();

        public double PreviewScale { get; set; } = 0.25;

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
            List<BitmapSource> frames = new List<BitmapSource>();

            foreach (var track in VideoTracks)
            {
                var trackFrame = track.GetFrameAt(frame);
                if (trackFrame != null)
                {
                    frames.Add(trackFrame);
                }
            }

            if (frames.Count == 0)
                return null;

            if (frames.Count == 1)
                return ScaleFrame(frames[0]);

            return CompositeImages(frames);
        }

        private BitmapSource CompositeImages(List<BitmapSource> frames)
        {
            if (frames.Count == 0)
                return null;

            int maxWidth = frames.Max(f => f.PixelWidth);
            int maxHeight = frames.Max(f => f.PixelHeight);

            Mat result = null;
            Mat scaledResult = null;

            try
            {
                result = new Mat(maxHeight, maxWidth, MatType.CV_8UC3, new Scalar(0, 0, 0));

                foreach (var frame in frames)
                {
                    using (Mat frameMat = BitmapSourceConverter.ToMat(frame))
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

                        int x = 0;
                        int y = 0;

                        // Ensure coordinates are within bounds
                        x = Math.Max(0, x);
                        y = Math.Max(0, y);

                        // Copy the frame to the result at its original size
                        OpenCvSharp.Rect roi = new OpenCvSharp.Rect(x, y, frameToLayer.Width, frameToLayer.Height);
                        Mat resultROI = new Mat(result, roi);
                        frameToLayer.CopyTo(resultROI);

                        if (frameToLayer != frameMat)
                        {
                            frameToLayer.Dispose();
                        }
                    }
                }

                if (PreviewScale < 1.0)
                {
                    scaledResult = new Mat();
                    int scaledWidth = (int)(maxWidth * PreviewScale);
                    int scaledHeight = (int)(maxHeight * PreviewScale);
                    Cv2.Resize(result, scaledResult, new OpenCvSharp.Size(scaledWidth, scaledHeight), 0, 0, InterpolationFlags.Area);

                    BitmapSource output = BitmapSourceConverter.ToBitmapSource(scaledResult);
                    output.Freeze();
                    return output;
                }
                else
                {
                    BitmapSource output = BitmapSourceConverter.ToBitmapSource(result);
                    output.Freeze();
                    return output;
                }
            }
            finally
            {
                result?.Dispose();
                scaledResult?.Dispose();
            }
        }

        private BitmapSource ScaleFrame(BitmapSource frame)
        {
            if (PreviewScale >= 1.0)
                return frame;

            using (Mat frameMat = BitmapSourceConverter.ToMat(frame))
            {
                int newWidth = (int)(frameMat.Width * PreviewScale);
                int newHeight = (int)(frameMat.Height * PreviewScale);

                using (Mat scaled = new Mat())
                {
                    Cv2.Resize(frameMat, scaled, new OpenCvSharp.Size(newWidth, newHeight), 0, 0, InterpolationFlags.Area);
                    BitmapSource output = BitmapSourceConverter.ToBitmapSource(scaled);
                    output.Freeze();
                    return output;
                }
            }
        }
    }
}