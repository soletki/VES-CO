using System.Diagnostics;
using System.Drawing.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using VESCO.Timeline;
using Xabe.FFmpeg;

namespace VESCO
{
    public class ClipManager
    {
        private readonly TimelineController _timelineController;
        private readonly Canvas _timelineCanvas;
        private VideoClip _selectedClip;
        private bool _isDragging;
        private Point _dragStartMouse;
        private long _dragStartFrame;

        public event Action<VideoClip> ClipSelected;
        public bool IsDragging => _isDragging;

        public ClipManager(TimelineController timelineController, Canvas timelineCanvas)
        {
            _timelineController = timelineController;
            _timelineCanvas = timelineCanvas;
        }

        public void AddClipAtPosition(SourceMedia source, double xPosition)
        {
            long startFrame = _timelineController.PositionToFrame(xPosition);

            var clip = new VideoClip(
                source.FilePath,
                sourceStartFrame: 0,
                timelineStartFrame: startFrame,
                source: source);

            _timelineController.Timeline.VideoTracks[0].AddClip(clip);
            UpdateClipPositions();
        }

        public void HandleTimelineClickSelect(Point position)
        {

            SelectClipAtPosition(position);

            if (_selectedClip != null)
            {
                _isDragging = true;
                _dragStartMouse = position;
                _dragStartFrame = _selectedClip.TimelineStart;
                _timelineCanvas.CaptureMouse();
            }
        }

        public void HandleTimelineClickCut(Point position)
        {
            CutClipAtPosition(position);
        }

        public void HandleDrag(Point position)
        {
            if (_selectedClip == null) return;

            double deltaX = position.X - _dragStartMouse.X;
            long totalFrames = _timelineController.GetTotalFramesWithBuffer();
            long deltaFrames = (long)((deltaX / _timelineCanvas.Width) * totalFrames);

            _selectedClip.TimelineStart = Math.Max(0, _dragStartFrame + deltaFrames);
            UpdateClipPositions();
        }

        public void EndDrag()
        {
            if (_isDragging)
            {
                _isDragging = false;
                _timelineCanvas.ReleaseMouseCapture();
                
                Debug.WriteLine($"Clip dropped: {_selectedClip?.Name}");
            }
        }

        public void UpdateClipPositions()
        {
            ClearClips();

            foreach (var clip in _timelineController.Timeline.VideoTracks[0].Clips)
            {
                DrawClip(clip);
            }

            HighlightSelectedClip();
        }

        private void SelectClipAtPosition(Point position)
        {
            long totalFrames = _timelineController.GetTotalFramesWithBuffer();

            foreach (var clip in _timelineController.Timeline.VideoTracks[0].Clips)
            {
                double clipX = _timelineController.FrameToPosition(clip.TimelineStart);
                double clipWidth = (clip.Length * (_timelineController.Timeline.Fps / clip.Source.FPS) / (double)totalFrames) * _timelineCanvas.Width;

                if (position.X >= clipX && position.X <= clipX + clipWidth)
                {
                    _selectedClip = clip;
                    HighlightSelectedClip();
                    ClipSelected?.Invoke(clip);
                    Debug.WriteLine($"Selected clip: {clip.Name}");
                    return;
                }
            }

            _selectedClip = null;
            ClearHighlights();
        }

        private void CutClipAtPosition(Point position)
        {
            long totalFrames = _timelineController.GetTotalFramesWithBuffer();

            Debug.WriteLine($"Attempting to cut at frame: {_timelineController.PositionToFrame(position.X)}");

            foreach (var clip in _timelineController.Timeline.VideoTracks[0].Clips)
            {
                double clipX = _timelineController.FrameToPosition(clip.TimelineStart);
                double clipWidth = (clip.Length * (_timelineController.Timeline.Fps / clip.Source.FPS) / (double)totalFrames) * _timelineCanvas.Width;

                if (position.X >= clipX && position.X <= clipX + clipWidth)
                {
                    long timelineCutFrame = _timelineController.PositionToFrame(position.X);
                    long cutFrame = (long)((timelineCutFrame - clip.TimelineStart) * (clip.Source.FPS / _timelineController.Timeline.Fps));
                    var (firstPart, secondPart) = clip.SplitAtFrame(cutFrame, _timelineController.Timeline);

                    if(firstPart == null || secondPart == null)
                    {
                        Debug.WriteLine($"Cut failed: Invalid split at frame {cutFrame} for clip {clip.Name}");
                        return;
                    }

                    _timelineController.Timeline.VideoTracks[0].RemoveClip(clip);
                    _timelineController.Timeline.VideoTracks[0].AddClip(firstPart);
                    _timelineController.Timeline.VideoTracks[0].AddClip(secondPart);
                    UpdateClipPositions();
                    Debug.WriteLine($"Cut clip: {clip.Name} at frame {cutFrame}");

                    return;
                }
            }
        }

        private void DrawClip(VideoClip clip)
        {
            long totalFrames = _timelineController.GetTotalFramesWithBuffer();
            double clipX = _timelineController.FrameToPosition(clip.TimelineStart);
            double clipWidth = (clip.Length * (_timelineController.Timeline.Fps / clip.Source.FPS) / (double)totalFrames) * _timelineCanvas.Width;

            var rect = new Border
            {
                Width = Math.Max(4, clipWidth),
                Height = 60,
                Background = new SolidColorBrush(Color.FromRgb(70, 130, 180)),
                BorderBrush = Brushes.Black,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(3),
                Child = new TextBlock
                {
                    Text = clip.Name,
                    Foreground = Brushes.White,
                    Margin = new Thickness(4, 2, 0, 0)
                }
            };

            Canvas.SetLeft(rect, clipX);
            Canvas.SetTop(rect, 10);

            clip.rect = rect;
            _timelineCanvas.Children.Add(rect);
        }

        private void HighlightSelectedClip()
        {
            if (_selectedClip?.rect != null)
            {
                ClearHighlights();
                _selectedClip.rect.BorderBrush = Brushes.Red;
            }
        }

        private void ClearHighlights()
        {
            foreach (var border in _timelineCanvas.Children.OfType<Border>())
            {
                border.BorderBrush = Brushes.Black;
            }
        }

        private void ClearClips()
        {
            var toRemove = _timelineCanvas.Children
                .OfType<UIElement>()
                .Where(e => e.GetType() == typeof(Border))
                .ToList();

            foreach (var element in toRemove)
            {
                _timelineCanvas.Children.Remove(element);
            }
        }
    }
}