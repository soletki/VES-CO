using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace VESCO
{
    public class PlayheadController
    {
        private readonly TimelineController _timelineController;
        private readonly UIElement _playhead;
        private readonly Image _previewImage;
        private long _currentFrame;
        private bool _isDragging;
        private bool _isPlaying;
        private DispatcherTimer _playbackTimer;
        private Stopwatch _playbackStopwatch;
        private long _playbackStartFrame;

        public bool IsDragging => _isDragging;
        public bool IsPlaying => _isPlaying;
        public long CurrentFrame => _currentFrame;

        public PlayheadController(TimelineController timelineController, UIElement playhead, Image previewImage)
        {
            _timelineController = timelineController;
            _playhead = playhead;
            _previewImage = previewImage;

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

            // Calculate elapsed frames based on actual time
            long elapsedMs = _playbackStopwatch.ElapsedMilliseconds;
            long targetFrame = _playbackStartFrame + (long)((elapsedMs / 1000.0) * _timelineController.Timeline.Fps);

            _currentFrame = targetFrame;

            // Stop at the end
            long maxFrame = _timelineController.GetTotalFramesWithBuffer();
            if (_currentFrame >= maxFrame)
            {
                Stop();
                _currentFrame = maxFrame - 1;
            }

            UpdatePlayheadPosition();
            UpdatePreview();
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
            Debug.WriteLine("Playback stopped");
        }

        public void StepForward()
        {
            if (_isPlaying) Pause();

            long maxFrame = _timelineController.GetTotalFramesWithBuffer();
            _currentFrame = Math.Min(maxFrame, _currentFrame + 1);
            UpdatePlayheadPosition();
            UpdatePreview();
            Debug.WriteLine($"Current frame: {_currentFrame}");
        }

        public void StepBackward()
        {
            if (_isPlaying) Pause();

            _currentFrame = Math.Max(0, _currentFrame - 1);
            UpdatePlayheadPosition();
            UpdatePreview();
            Debug.WriteLine($"Current frame: {_currentFrame}");
        }

        public void UpdatePlayheadPosition()
        {
            double x = _timelineController.FrameToPosition(_currentFrame);
            Canvas.SetLeft(_playhead, x);
        }

        public void UpdatePreview()
        {
            _previewImage.Source = _timelineController.Timeline.GetFrameAtFrame(_currentFrame);
        }

        public void StartDragging(Point position)
        {
            if (_isPlaying) Pause();

            _isDragging = true;
            _playhead.CaptureMouse();
            UpdateFromMouse(position);
        }

        public void EndDragging()
        {
            _isDragging = false;
            _playhead.ReleaseMouseCapture();
        }

        public void UpdateFromMouse(Point position)
        {
            _currentFrame = _timelineController.PositionToFrame(position.X);
            UpdatePlayheadPosition();
            UpdatePreview();
        }
    }
}