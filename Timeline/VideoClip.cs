using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using System.Collections.Concurrent;
using System.Diagnostics;
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
        private readonly FrameCache<long, BitmapSource> _frameCache;
        private readonly ConcurrentQueue<long> _cachePriorityQueue = new();
        private CancellationTokenSource _cachingCancellationTokenSource;
        private Task _cachingTask;
        private readonly object _cacheLockObj = new();
        private long _currentPlayheadFrame = -1;
        private const int CacheSize = 400;
        private const int EstimatedFrameMemoryBytes = 3_000_000; // ~3MB per frame

        public int X { get; set; }
        public int Y { get; set; }
        public double Scale { get; set; } = 1.0;
        public double Opacity { get; set; } = 1.0;

        public VideoSource Source { get; }
        public long SourceStart { get; }
        public long Length { get; }

        public VideoClip(string filePath, long sourceStart, long timelineStart, VideoSource source, double timelineFps, long length = -1, int x = 0, int y = 0, double scale = 1.0, double opacity = 1.0)
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

            _frameCache = new FrameCache<long, BitmapSource>(EstimatedFrameMemoryBytes * CacheSize);
            
            _cachingCancellationTokenSource = new CancellationTokenSource();
            _cachingTask = Task.Run(() => BackgroundFrameCachingLoop(_cachingCancellationTokenSource.Token));
        }

        public BitmapSource GetFrameAtTimelineFrame(long timelineFrame, double scale)
        {
            long localFrame = SourceStart + (long)Math.Round((timelineFrame - TimelineStart) * (Source.FPS / _timelineFps));
            lock (_cacheLockObj)
            {
                _currentPlayheadFrame = localFrame;
                Debug.WriteLine(_currentPlayheadFrame);
            }

            if (_frameCache.TryGet(localFrame, out var cachedFrame))
            {
                return cachedFrame;
            }

            return ReadFrameDirectly(localFrame, scale);
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
                    lock (_cacheLockObj)
                    {
                        currentFrame = _currentPlayheadFrame;
                    }

                    if (currentFrame >= 0)
                    {
                        long startFrame = Math.Max(SourceStart, currentFrame);
                        long endFrame = Math.Min(SourceStart + Length - 1, currentFrame + CacheSize);

                        for (long offset = 0; offset <= CacheSize; offset++)
                        {
                            lock (_cacheLockObj)
                            {
                                if(currentFrame!=_currentPlayheadFrame)
                                    break;
                            }
                            if (cancellationToken.IsCancellationRequested)
                                break;

                            // Cache frame ahead
                            long frameAhead = currentFrame + offset;
                            if (frameAhead >= SourceStart && frameAhead < SourceStart + Length && !_frameCache.TryGet(frameAhead, out _))
                            {
                                CacheFrame(frameAhead);
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
                    // Silently handle any errors in background caching
                }
            }
        }

        private void CacheFrame(long localFrame)
        {
            try
            {
                using (var mat = new Mat())
                {
                    if (_lastCachedFrame != localFrame - 1)
                    {
                        _cacheCapture.Set(VideoCaptureProperties.PosFrames, localFrame);
                    }
                    
                    
                    if (!_cacheCapture.Read(mat) || mat.Empty())
                        return;

                    BitmapSource bitmap = mat.ToBitmapSource();
                    bitmap?.Freeze();

                    _frameCache.Add(localFrame, bitmap, EstimatedFrameMemoryBytes);
                    _cachePriorityQueue.Append(localFrame);
                }
            }
            catch
            {
                // Silently handle caching errors
            }
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
                Opacity
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
                Opacity
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
