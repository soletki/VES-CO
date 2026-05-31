using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using Microsoft.Extensions.Logging;
using System.Windows.Media.Imaging;

namespace VESCO.Timeline
{
    public class VideoClip : Clip, IDisposable
    {
        private VideoCapture _capture;
        private readonly VideoCapture _cacheCapture;
        private long _lastFrame = -1;
        private double _timelineFps;

        // Frame cache system
        private long _lastCachedFrame = -1;
        private readonly FrameCache<(long Frame, int ScaleBucket), BitmapSource> _frameCache;
        private CancellationTokenSource _cachingCancellationTokenSource;
        private Task _cachingTask;
        private readonly object _cacheLockObj = new();
        private readonly ILogger<VideoClip> _logger;
        private long _currentPlayheadFrame = -1;
        private double _currentPreviewScale = 1.0;
        private const int CacheSize = 400;
        private const int EstimatedFrameMemoryBytes = 3_000_000; // ~3MB per frame
        private const int ScaleBucketMultiplier = 1000;

        public int X { get; set; }
        public int Y { get; set; }
        public double Scale { get; set; } = 1.0;
        public double Opacity { get; set; } = 1.0;

        public VideoSource Source { get; }
        public long SourceStart { get; }
        public long Length { get; }

        public VideoClip(string filePath, long sourceStart, long timelineStart, VideoSource source, double timelineFps, long length = -1, int x = 0, int y = 0, double scale = 1.0, double opacity = 1.0, ILogger<VideoClip>? logger = null)
            : base(filePath, timelineStart)
        {
            _capture = new VideoCapture(source.FilePath);
            _cacheCapture = new VideoCapture(source.FilePath);
            _timelineFps = timelineFps;
            Source = source;
            SourceStart = sourceStart;
            X = x;
            Y = y;
            Scale = scale;
            Opacity = opacity;
            Length = length != -1 ? length : Source.FrameCount - SourceStart;
            _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<VideoClip>.Instance;

            _frameCache = new FrameCache<(long Frame, int ScaleBucket), BitmapSource>(EstimatedFrameMemoryBytes * CacheSize);
            
            _cachingCancellationTokenSource = new CancellationTokenSource();
            _cachingTask = Task.Run(() => BackgroundFrameCachingLoop(_cachingCancellationTokenSource.Token));
        }

        public BitmapSource GetFrameAtTimelineFrame(long timelineFrame, double scale)
        {
            double normalizedScale = NormalizeScale(scale);
            long localFrame = SourceStart + (long)Math.Round((timelineFrame - TimelineStart) * (Source.FPS / _timelineFps));
            var cacheKey = CreateCacheKey(localFrame, normalizedScale);
            lock (_cacheLockObj)
            {
                _currentPlayheadFrame = localFrame;
                _currentPreviewScale = normalizedScale;
            }
            _logger.LogTrace("Requested video frame {LocalFrame} with scale {Scale}", localFrame, normalizedScale);

            if (_frameCache.TryGet(cacheKey, out var cachedFrame))
            {
                return cachedFrame;
            }

            BitmapSource frame = ReadFrameDirectly(localFrame, normalizedScale);
            if (frame != null)
            {
                if (frame.CanFreeze)
                    frame.Freeze();
                _frameCache.Add(cacheKey, frame, EstimateFrameMemoryBytes(normalizedScale));
            }

            return frame;
        }

        private BitmapSource ReadFrameDirectly(long localFrame, double scale)
        {
            localFrame = Math.Clamp(localFrame, 0, Source.FrameCount - 1);

            if (_lastFrame != localFrame - 1)
            {
                _capture.Set(VideoCaptureProperties.PosFrames, localFrame);
            }

            using var mat = new Mat();
            if (!_capture.Read(mat) || mat.Empty())
                return null;

            _lastFrame = localFrame;

            Cv2.Resize(mat, mat, new Size(mat.Width * scale, mat.Height * scale), 0, 0, InterpolationFlags.Area);

            BitmapSource bitmap = mat.ToBitmapSource();
            return bitmap;
        }

        private void BackgroundFrameCachingLoop(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    long currentFrame;
                    double currentScale;
                    int currentScaleBucket;
                    lock (_cacheLockObj)
                    {
                        currentFrame = _currentPlayheadFrame;
                        currentScale = _currentPreviewScale;
                        currentScaleBucket = GetScaleBucket(currentScale);
                    }

                    if (currentFrame >= 0)
                    {
                        for (long offset = 0; offset <= CacheSize; offset++)
                        {
                            lock (_cacheLockObj)
                            {
                                if (currentFrame != _currentPlayheadFrame || currentScaleBucket != GetScaleBucket(_currentPreviewScale))
                                    break;
                            }
                            if (cancellationToken.IsCancellationRequested)
                                break;

                            // Cache frame ahead
                            long frameAhead = currentFrame + offset;
                            var cacheKey = CreateCacheKey(frameAhead, currentScale);
                            if (frameAhead >= SourceStart && frameAhead < SourceStart + Length && !_frameCache.TryGet(cacheKey, out _))
                            {
                                CacheFrame(frameAhead, currentScale);
                            }
                        }
                    }

                    // Sleep briefly to avoid busy-waiting
                    Task.Delay(50, cancellationToken).Wait(cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch
                {
                    _logger.LogDebug("Video frame caching loop hit an internal error for {FilePath}", FilePath);
                }
            }
        }

        private void CacheFrame(long localFrame, double scale)
        {
            try
            {
                double normalizedScale = NormalizeScale(scale);
                var cacheKey = CreateCacheKey(localFrame, normalizedScale);

                using (var mat = new Mat())
                {
                    if (_lastCachedFrame != localFrame - 1)
                    {
                        _cacheCapture.Set(VideoCaptureProperties.PosFrames, localFrame);
                    }

                    if (!_cacheCapture.Read(mat) || mat.Empty())
                        return;

                    _lastCachedFrame = localFrame;

                    if (normalizedScale != 1.0)
                    {
                        Cv2.Resize(mat, mat, new Size(mat.Width * normalizedScale, mat.Height * normalizedScale), 0, 0, InterpolationFlags.Area);
                    }

                    BitmapSource bitmap = mat.ToBitmapSource();
                    if (bitmap.CanFreeze)
                        bitmap.Freeze();

                    _frameCache.Add(cacheKey, bitmap, EstimateFrameMemoryBytes(normalizedScale));
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to cache frame {LocalFrame} for {FilePath}", localFrame, FilePath);
            }
        }

        private static int GetScaleBucket(double scale)
        {
            return (int)Math.Round(NormalizeScale(scale) * ScaleBucketMultiplier);
        }

        private static (long Frame, int ScaleBucket) CreateCacheKey(long frame, double scale)
        {
            return (frame, GetScaleBucket(scale));
        }

        private static double NormalizeScale(double scale)
        {
            return Math.Clamp(scale, 0.01, 8.0);
        }

        private static long EstimateFrameMemoryBytes(double scale)
        {
            double normalizedScale = NormalizeScale(scale);
            return (long)Math.Max(250_000, EstimatedFrameMemoryBytes * normalizedScale * normalizedScale);
        }

        public (VideoClip? first, VideoClip? second) SplitAtFrame(long splitFrame, Timeline timeline)
        {
            if (splitFrame <= 0 || splitFrame >= Length)
                return (this, null);

            var firstClip = new VideoClip(
                FilePath,
                SourceStart,
                TimelineStart,
                new VideoSource(Source.FilePath),
                _timelineFps,
                splitFrame,
                X,
                Y,
                Scale,
                Opacity,
                _logger
            );

            var secondClip = new VideoClip(
                FilePath,
                splitFrame,
                (long)(TimelineStart + splitFrame * (timeline.Fps / Source.FPS)),
                new VideoSource(Source.FilePath),
                _timelineFps,
                Length - splitFrame,
                X,
                Y,
                Scale,
                Opacity,
                _logger
            );

            return (firstClip, secondClip);
        }

        public void Dispose()
        {
            _cachingCancellationTokenSource?.Cancel();
            try
            {
                _cachingTask?.Wait(5000);
            }
            catch { }

            _cachingCancellationTokenSource?.Dispose();
            _frameCache?.Dispose();
            _capture?.Dispose();
            _cacheCapture?.Dispose();
        }
    }
}
