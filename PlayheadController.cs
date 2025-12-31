using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace VESCO
{
    public class PlayheadController
    {
        private readonly TimelineController _timelineController;
        private readonly UIElement _playhead;
        private readonly Image _previewImage;
        private long _currentFrame;
        private bool _isDragging;

        public bool IsDragging => _isDragging;

        public PlayheadController(TimelineController timelineController, UIElement playhead, Image previewImage)
        {
            _timelineController = timelineController;
            _playhead = playhead;
            _previewImage = previewImage;
        }

        public void StepForward()
        {
            long maxFrame = _timelineController.GetTotalFramesWithBuffer();
            _currentFrame = Math.Min(maxFrame, _currentFrame + 1);
            UpdatePlayheadPosition();
            UpdatePreview();
            Debug.WriteLine($"Current frame: {_currentFrame}");
        }

        public void StepBackward()
        {
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