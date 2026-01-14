using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using VESCO.Timeline;

namespace VESCO
{
    public class ClipManager
    {
        private readonly TimelineController _timelineController;
        private readonly Canvas _timelineCanvas;
        private readonly StackPanel _trackLabelsPanel;
        private readonly TextBox _xTextBox;
        private readonly TextBox _yTextBox;
        private readonly TextBox _scaleTextBox;
        private readonly TextBox _opacityTextBox;
        private VideoClip _selectedClip;
        private int _selectedTrackIndex = -1;
        private bool _isDragging;
        private Point _dragStartMouse;
        private long _dragStartFrame;
        private int TrackHeight = 40;

        public event Action<VideoClip> ClipSelected;
        public bool IsDragging => _isDragging;

        public ClipManager(TimelineController timelineController, Canvas timelineCanvas, StackPanel trackLabelsPanel, TextBox xTextBox, TextBox yTextBox, TextBox scaleTextBox, TextBox opacityTextBox)
        {
            _timelineController = timelineController;
            _timelineCanvas = timelineCanvas;
            _trackLabelsPanel = trackLabelsPanel;
            _xTextBox = xTextBox;
            _yTextBox = yTextBox;
            _scaleTextBox = scaleTextBox;
            _opacityTextBox = opacityTextBox;
        }

        public void IncreaseTrackHeight()
        {
            TrackHeight += 10;

            UpdateTimelineHeight();
            UpdateClipPositions();
            UpdateLabelsHeight();
        }

        public void DecreaseTrackHeight()
        {
            TrackHeight = Math.Max(10, TrackHeight-10);

            UpdateTimelineHeight();
            UpdateClipPositions();
            UpdateLabelsHeight();
        }

        public void DeleteSelectedClip()
        {
            if (_selectedClip != null && _selectedTrackIndex >= 0)
            {
                _timelineController.Timeline.VideoTracks[_selectedTrackIndex].RemoveClip(_selectedClip);
                _selectedClip = null;
                _selectedTrackIndex = -1;
                UpdateClipPositions();
            }
        }

        public void InitializeTracks()
        {
            for (int i = 0; i < 2; i++)
            {
                AddVideoTrack();
            }

            UpdateTimelineHeight();
        }

        public void AddVideoTrack()
        {
            int trackIndex = _timelineController.Timeline.VideoTracks.Count;
            string trackName = $"V{trackIndex + 1}";

            var track = new VideoTrack(trackName, _timelineController.Timeline.Fps);
            _timelineController.Timeline.VideoTracks.Insert(0, track);

            // Create track label with eye icon
            var labelGrid = new Grid();
            labelGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });
            labelGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var eyeButton = new Border
            {
                Width = 20,
                Height = 20,
                Background = Brushes.Transparent,
                Child = new TextBlock
                {
                    Text = "👁",
                    FontSize = 14,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                },
                Cursor = System.Windows.Input.Cursors.Hand
            };
            Grid.SetColumn(eyeButton, 0);

            var trackNameText = new TextBlock
            {
                Text = trackName,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                FontWeight = FontWeights.Bold
            };
            Grid.SetColumn(trackNameText, 1);

            labelGrid.Children.Add(eyeButton);
            labelGrid.Children.Add(trackNameText);

            var labelBorder = new Border
            {
                Height = TrackHeight,
                Background = new SolidColorBrush(Color.FromRgb(45, 45, 48)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(63, 63, 70)),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Child = labelGrid
            };

            _trackLabelsPanel.Children.Insert(0, labelBorder);

            UpdateTimelineHeight();
            UpdateClipPositions();
        }

        private void UpdateTimelineHeight()
        {
            int trackCount = _timelineController.Timeline.VideoTracks.Count;
            double totalHeight = trackCount * (TrackHeight);

            double extraHeight = 20;

            _timelineCanvas.Height = totalHeight;
            _trackLabelsPanel.Height = totalHeight;

            // Update playhead height to match canvas height
            var playhead = _timelineCanvas.Children.OfType<System.Windows.Shapes.Rectangle>()
                .FirstOrDefault(r => r.Name == "Playhead");
            if (playhead != null)
            {
                playhead.Height = totalHeight + extraHeight;
            }
        }

        public void AddClipAtPosition(SourceMedia source, double xPosition, double yPosition)
        {
            int trackIndex = GetTrackIndexFromY(yPosition);
            if (trackIndex < 0 || trackIndex >= _timelineController.Timeline.VideoTracks.Count)
                return;

            long startFrame = _timelineController.PositionToFrame(xPosition);

            var clip = new VideoClip(
                source.FilePath,
                sourceStart: 0,
                timelineStart: startFrame,
                source: source);

            _timelineController.Timeline.VideoTracks[trackIndex].AddClip(clip);
            UpdateClipPositions();
        }

        public void HandleTimelineClickSelect(Point position)
        {
            SelectClipAtPosition(position);

            if (_selectedClip != null)
            {
                _isDragging = true;
                _dragStartMouse = position;
                _dragStartFrame = _selectedClip.timelineStart;
                _timelineCanvas.CaptureMouse();
            }
        }

        public void HandleTimelineClickCut(Point position)
        {
            CutClipAtPosition(position);
        }

        public void HandleDrag(Point position)
        {
            if (_selectedClip == null || _selectedTrackIndex < 0) return;

            double deltaX = position.X - _dragStartMouse.X;
            long totalFrames = _timelineController.GetTotalFramesWithBuffer();
            long deltaFrames = (long)((deltaX / _timelineCanvas.Width) * totalFrames);

            _selectedClip.timelineStart = Math.Max(0, _dragStartFrame + deltaFrames);

            // Check if dragged to a different track
            int newTrackIndex = GetTrackIndexFromY(position.Y);
            if (newTrackIndex >= 0 && newTrackIndex < _timelineController.Timeline.VideoTracks.Count && newTrackIndex != _selectedTrackIndex)
            {
                // Move clip to new track
                _timelineController.Timeline.VideoTracks[_selectedTrackIndex].RemoveClip(_selectedClip);
                _timelineController.Timeline.VideoTracks[newTrackIndex].AddClip(_selectedClip);
                _selectedTrackIndex = newTrackIndex;
            }

            UpdateClipPositions();
        }

        public void EndDrag()
        {
            if (_isDragging)
            {
                _isDragging = false;
                _timelineCanvas.ReleaseMouseCapture();
                Debug.WriteLine($"Clip dropped: {_selectedClip?.name}");
            }
        }

        public void UpdateClipPositions()
        {
            ClearClips();

            for (int trackIndex = 0; trackIndex < _timelineController.Timeline.VideoTracks.Count; trackIndex++)
            {
                var track = _timelineController.Timeline.VideoTracks[trackIndex];
                foreach (var clip in track.Clips)
                {
                    DrawClip(clip, trackIndex);
                }
            }

            HighlightSelectedClip();
        }

        public void SelectClipAtPosition(Point position)
        {
            int trackIndex = GetTrackIndexFromY(position.Y);
            if (trackIndex < 0 || trackIndex >= _timelineController.Timeline.VideoTracks.Count)
            {
                _selectedClip = null;
                _selectedTrackIndex = -1;
                ClearHighlights();
                return;
            }

            long totalFrames = _timelineController.GetTotalFramesWithBuffer();
            var track = _timelineController.Timeline.VideoTracks[trackIndex];

            foreach (var clip in track.Clips)
            {
                double clipX = _timelineController.FrameToPosition(clip.timelineStart);
                double clipWidth = (clip.length * (_timelineController.Timeline.Fps / clip.source.FPS) / (double)totalFrames) * _timelineCanvas.Width;

                if (position.X >= clipX && position.X <= clipX + clipWidth)
                {
                    _selectedClip = clip;
                    _xTextBox.IsEnabled = true;
                    _xTextBox.Text = _selectedClip.x.ToString();
                    _yTextBox.IsEnabled = true;
                    _yTextBox.Text = _selectedClip.y.ToString();
                    _scaleTextBox.IsEnabled = true;
                    _scaleTextBox.Text = _selectedClip.scale.ToString("F2");
                    _opacityTextBox.IsEnabled = true;
                    _opacityTextBox.Text = _selectedClip.opacity.ToString("F2");
                    _selectedTrackIndex = trackIndex;
                    HighlightSelectedClip();
                    ClipSelected?.Invoke(clip);
                    Debug.WriteLine($"Selected clip: {clip.name} on track {trackIndex}");
                    return;
                }
            }

            _selectedClip = null;
            _xTextBox.IsEnabled = false;
            _yTextBox.IsEnabled = false;
            _scaleTextBox.IsEnabled = false;
            _opacityTextBox.IsEnabled = false;
            _selectedTrackIndex = -1;
            ClearHighlights();
        }

        private void CutClipAtPosition(Point position)
        {
            int trackIndex = GetTrackIndexFromY(position.Y);
            if (trackIndex < 0 || trackIndex >= _timelineController.Timeline.VideoTracks.Count)
                return;

            long totalFrames = _timelineController.GetTotalFramesWithBuffer();
            var track = _timelineController.Timeline.VideoTracks[trackIndex];

            Debug.WriteLine($"Attempting to cut at frame: {_timelineController.PositionToFrame(position.X)}");

            foreach (var clip in track.Clips.ToList())
            {
                double clipX = _timelineController.FrameToPosition(clip.timelineStart);
                double clipWidth = (clip.length * (_timelineController.Timeline.Fps / clip.source.FPS) / (double)totalFrames) * _timelineCanvas.Width;

                if (position.X >= clipX && position.X <= clipX + clipWidth)
                {
                    long timelineCutFrame = _timelineController.PositionToFrame(position.X);
                    long cutFrame = (long)((timelineCutFrame - clip.timelineStart) * (clip.source.FPS / _timelineController.Timeline.Fps));
                    var (firstPart, secondPart) = clip.SplitAtFrame(cutFrame, _timelineController.Timeline);

                    if (firstPart == null || secondPart == null)
                    {
                        Debug.WriteLine($"Cut failed: Invalid split at frame {cutFrame} for clip {clip.name}");
                        return;
                    }

                    track.RemoveClip(clip);
                    track.AddClip(firstPart);
                    track.AddClip(secondPart);
                    UpdateClipPositions();
                    Debug.WriteLine($"Cut clip: {clip.name} at frame {cutFrame}");
                    return;
                }
            }
        }

        private int GetTrackIndexFromY(double y)
        {
            return (int)(y / (TrackHeight));
        }

        private double GetTrackY(int trackIndex)
        {
            return trackIndex * (TrackHeight);
        }

        private void DrawClip(VideoClip clip, int trackIndex)
        {
            long totalFrames = _timelineController.GetTotalFramesWithBuffer();
            double clipX = _timelineController.FrameToPosition(clip.timelineStart);
            double clipWidth = (clip.length * (_timelineController.Timeline.Fps / clip.source.FPS) / (double)totalFrames) * _timelineCanvas.Width;

            Color[] trackColors = new[]
            {
                Color.FromRgb(70, 130, 180),
                Color.FromRgb(180, 130, 70),
                Color.FromRgb(130, 180, 70),
                Color.FromRgb(180, 70, 130)
            };

            var rect = new Border
            {
                Width = Math.Max(4, clipWidth),
                Height = TrackHeight,
                Background = new SolidColorBrush(trackColors[trackIndex % trackColors.Length]),
                BorderBrush = Brushes.Black,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(3),
                Child = new TextBlock
                {
                    Text = clip.name,
                    Foreground = Brushes.White,
                    Margin = new Thickness(4, 2, 0, 0)
                }
            };

            Canvas.SetLeft(rect, clipX);
            Canvas.SetTop(rect, GetTrackY(trackIndex));

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

        private void UpdateLabelsHeight()
        {
            for (int i = 0; i < _trackLabelsPanel.Children.Count; i++)
            {
                if (_trackLabelsPanel.Children[i] is Border border)
                {
                    border.Height = TrackHeight;
                }
            }
        }

        public void UpdateSelectedClipX(int x)
        {
            if (_selectedClip != null)
            {
                _selectedClip.x = x;
            }
        }

        public void UpdateSelectedClipY(int y)
        {
            if (_selectedClip != null)
            {
                _selectedClip.y = y;
            }
        }

        public void UpdateSelectedClipScale(double scale)
        {
            if (_selectedClip != null)
            {
                _selectedClip.scale = scale;
            }
        }

        public void UpdateSelectedClipOpacity(double opacity)
        {
            if (_selectedClip != null)
            {
                _selectedClip.opacity = opacity;
            }
        }
    }
}