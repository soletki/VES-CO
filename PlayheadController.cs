using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace VESCO
{
    public class PlayheadController
    {
        private readonly TimelineController _timelineController;
        private readonly Canvas _playheadCanvas;
        private readonly Rectangle _playhead;
        private readonly Polygon _playheadTop;
        private readonly Image _previewImage;
        private readonly TextBlock _timecodeDisplay;
        private readonly TextBlock _frameCounter;
        private readonly ScrollViewer _timelineScrollViewer;
        private long _currentFrame;
        private bool _isDragging;
        private bool _isPlaying;
        private DispatcherTimer _playbackTimer;
        private Stopwatch _playbackStopwatch;
        private long _playbackStartFrame;

        public bool IsDragging => _isDragging;
        public bool IsPlaying => _isPlaying;
        public long CurrentFrame => _currentFrame;

        public PlayheadController(TimelineController timelineController, Canvas playheadCanvas,
            Rectangle playhead, Polygon playheadTop, Image previewImage,
            TextBlock timecodeDisplay, TextBlock frameCounter, ScrollViewer timelineScrollViewer)
        {
            _timelineController = timelineController;
            _timelineScrollViewer = timelineScrollViewer;
            _playheadCanvas = playheadCanvas;
            _playhead = playhead;
            _playheadTop = playheadTop;
            _previewImage = previewImage;
            _timecodeDisplay = timecodeDisplay;
            _frameCounter = frameCounter;

            InitializePlaybackTimer();
        }

        private void InitializePlaybackTimer()
        {
            _playbackTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(1000.0 / _timelineController.Timeline.Fps)
            };
            _playbackTimer.Tick += PlaybackTimer_Tick;
            _playbackStopwatch = new Stopwatch();
        }

        private void PlaybackTimer_Tick(object sender, EventArgs e)
        {
            if (!_isPlaying) return;

            long elapsedMs = _playbackStopwatch.ElapsedMilliseconds;
            long targetFrame = _playbackStartFrame + (long)((elapsedMs / 1000.0) * _timelineController.Timeline.Fps);

            _currentFrame = targetFrame;

            long maxFrame = _timelineController.GetTotalFramesWithBuffer();
            if (_currentFrame >= maxFrame)
            {
                Stop();
                _currentFrame = maxFrame - 1;
            }

            UpdatePlayheadPosition();
            AutoScrollIfNeeded();
            UpdatePreview();
            UpdateDisplays();
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
            if (_isPlaying) return;

            _isPlaying = true;
            _playbackStartFrame = _currentFrame;
            _playbackStopwatch.Restart();
            _playbackTimer.Start();
            Debug.WriteLine("Playback started");
        }

        public void Pause()
        {
            if (!_isPlaying) return;

            _isPlaying = false;
            _playbackTimer.Stop();
            _playbackStopwatch.Stop();
            Debug.WriteLine("Playback paused");
        }

        public void Stop()
        {
            _isPlaying = false;
            _playbackTimer.Stop();
            _playbackStopwatch.Stop();
            _currentFrame = 0;
            UpdatePlayheadPosition();
            UpdatePreview();
            UpdateDisplays();
            Debug.WriteLine("Playback stopped");
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
            if (x <= -10) _playheadCanvas.Visibility = System.Windows.Visibility.Hidden;
            else _playheadCanvas.Visibility = System.Windows.Visibility.Visible;
                Canvas.SetLeft(_playheadCanvas, x);
        }

        public void UpdatePreview()
        {
            _previewImage.Source = _timelineController.Timeline.GetFrameAtFrame(_currentFrame);
        }

        public void UpdateDisplays()
        {
            // Update timecode (HH:MM:SS:FF)
            double fps = _timelineController.Timeline.Fps;
            int frames = (int)(_currentFrame % fps);
            int totalSeconds = (int)(_currentFrame / fps);
            int seconds = totalSeconds % 60;
            int minutes = (totalSeconds / 60) % 60;
            int hours = totalSeconds / 3600;

            _timecodeDisplay.Text = $"{hours:D2}:{minutes:D2}:{seconds:D2}:{frames:D2}";

            // Update frame counter
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