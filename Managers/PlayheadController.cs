using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;

namespace VESCO.Managers
{
    public class PlayheadController
    {
        private readonly TimelineController _timelineController;
        private readonly Canvas _playheadCanvas;
        private readonly Image _previewImage;
        private readonly TextBlock _timecodeDisplay;
        private readonly TextBlock _frameCounter;
        private readonly ScrollViewer _timelineScrollViewer;
        private readonly Dispatcher _dispatcher;
        private readonly ILogger<PlayheadController> _logger;
        private long _currentFrame;
        private bool _isDragging;
        private bool _isPlaying;
        private Stopwatch? _playbackStopwatch;
        private long _playbackStartFrame;
        private CancellationTokenSource? _previewCts;
        private CancellationTokenSource? _playbackCts;
        private readonly object _playbackLock = new();
        private readonly object _previewLock = new();

        public bool IsDragging => _isDragging;
        public bool IsPlaying => _isPlaying;
        public long CurrentFrame => _currentFrame;

        public PlayheadController(TimelineController timelineController, Canvas playheadCanvas,
            Rectangle playhead, Polygon playheadTop, Image previewImage,
            TextBlock timecodeDisplay, TextBlock frameCounter, ScrollViewer timelineScrollViewer,
            ILogger<PlayheadController> logger)
        {
            _timelineController = timelineController;
            _timelineScrollViewer = timelineScrollViewer;
            _playheadCanvas = playheadCanvas;
            _previewImage = previewImage;
            _timecodeDisplay = timecodeDisplay;
            _frameCounter = frameCounter;
            _dispatcher = Dispatcher.CurrentDispatcher;
            _logger = logger;

            _playbackStopwatch = new Stopwatch();
        }

        private async void RunPlaybackLoop(CancellationToken cancellationToken)
        {
            double frameIntervalMs = 1000.0 / _timelineController.Timeline.Fps;

            try
            {
                while (!cancellationToken.IsCancellationRequested && _isPlaying)
                {
                    lock (_playbackLock)
                    {
                        if (!_isPlaying)
                            break;

                        long elapsedMs = _playbackStopwatch.ElapsedMilliseconds;
                        long targetFrame = _playbackStartFrame + (long)(elapsedMs / 1000.0 * _timelineController.Timeline.Fps);

                        _currentFrame = targetFrame;

                        long maxFrame = _timelineController.GetTotalFramesWithBuffer();
                        if (_currentFrame >= maxFrame)
                        {
                            _isPlaying = false;
                            _currentFrame = maxFrame - 1;
                            _playbackStopwatch.Stop();
                        }
                    }

                    // Dispatch UI updates to the UI thread
                    _dispatcher.Invoke(() =>
                    {
                        if (_isPlaying)
                        {
                            UpdatePlayheadPosition();
                            AutoScrollIfNeeded();
                            UpdatePreview();
                            UpdateDisplays();
                        }
                    });

                    await Task.Delay((int)frameIntervalMs, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("Playback loop cancelled");
            }
        }

        private void AutoScrollIfNeeded()
        {
            double playheadX = _timelineController.FrameToPosition(_currentFrame);

            double left = _timelineScrollViewer.HorizontalOffset;
            double right = left + _timelineScrollViewer.ViewportWidth;

            if (playheadX < left)
            {
                _timelineScrollViewer.ScrollToHorizontalOffset(
                    Math.Max(0, playheadX));
            }
            else if (playheadX > right)
            {
                _timelineScrollViewer.ScrollToHorizontalOffset(
                    playheadX);
            }
        }


        public void UpdateCurrentFrame(long frame)
        {
            _currentFrame = frame;
        }

        public void TogglePlayback()
        {
            if (_isPlaying)
            {
                Pause();
            }
            else
            {
                Play();
            }
        }

        public void Play()
        {
            lock (_playbackLock)
            {
                if (_isPlaying) return;

                _isPlaying = true;
                _playbackStartFrame = _currentFrame;
                _playbackStopwatch.Restart();
                
                _playbackCts = new CancellationTokenSource();
                _ = Task.Run(() => RunPlaybackLoop(_playbackCts.Token));
                _logger.LogDebug("Playback started");
            }
        }

        public void Pause()
        {
            lock (_playbackLock)
            {
                if (!_isPlaying) return;

                _isPlaying = false;
                _playbackStopwatch.Stop();
                _playbackCts?.Cancel();
                _logger.LogDebug("Playback paused");
            }
        }

        public void Stop()
        {
            lock (_playbackLock)
            {
                _isPlaying = false;
                _playbackStopwatch.Stop();
                _playbackCts?.Cancel();
                _currentFrame = 0;
            }

            _dispatcher.Invoke(() =>
            {
                UpdatePlayheadPosition();
                UpdatePreview();
                UpdateDisplays();
            });
            _logger.LogDebug("Playback stopped");
        }

        public void StepForward()
        {
            if (_isPlaying) Pause();

            long maxFrame = _timelineController.GetTotalFramesWithBuffer();
            _currentFrame = Math.Min(maxFrame, _currentFrame + 1);
            UpdatePlayheadPosition();
            UpdatePreview();
            UpdateDisplays();
        }

        public void StepBackward()
        {
            if (_isPlaying) Pause();

            _currentFrame = Math.Max(0, _currentFrame - 1);
            UpdatePlayheadPosition();
            UpdatePreview();
            UpdateDisplays();
        }

        public void UpdatePlayheadPosition()
        {
            double x = _timelineController.FrameToPosition(_currentFrame) - _timelineScrollViewer.HorizontalOffset;
            if (x <= -10) _playheadCanvas.Visibility = Visibility.Hidden;
            else _playheadCanvas.Visibility = Visibility.Visible;
            Canvas.SetLeft(_playheadCanvas, x);
        }

        public void UpdatePreview()
        {
            _previewImage.Source = _timelineController.Timeline.GetFrameAtFrame(_currentFrame);
        }

        public void UpdateDisplays()
        {
            double fps = _timelineController.Timeline.Fps;
            int frames = (int)(_currentFrame % fps);
            int totalSeconds = (int)(_currentFrame / fps);
            int seconds = totalSeconds % 60;
            int minutes = totalSeconds / 60 % 60;
            int hours = totalSeconds / 3600;

            _timecodeDisplay.Text = $"{hours:D2}:{minutes:D2}:{seconds:D2}:{frames:D2}";

            long totalFrames = _timelineController.Timeline.GetTotalFrames();
            _frameCounter.Text = $"{_currentFrame} / {totalFrames}";
        }

        public void StartDragging(Point position)
        {
            if (_isPlaying) Pause();

            _isDragging = true;
            UpdateFromMouse(position);
        }

        public void EndDragging()
        {
            _isDragging = false;
        }

        public void UpdateFromMouse(Point position)
        {
            _currentFrame = _timelineController.PositionToFrame(position.X);
            UpdatePlayheadPosition();
            UpdatePreview();
            UpdateDisplays();
        }
    }
}
