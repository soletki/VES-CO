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
        private Clip _selectedClip;
        private int _selectedTrackIndex = -1;
        private bool _isDragging;
        private Point _dragStartMouse;
        private long _dragStartFrame;
        private int TrackHeight = 40;

        public event Action<Clip> ClipSelected;
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
                if(_selectedClip is AudioClip)
                    _timelineController.Timeline.AudioTracks[_selectedTrackIndex].RemoveClip((AudioClip)_selectedClip);
                else
                    _timelineController.Timeline.VideoTracks[_selectedTrackIndex].RemoveClip((VideoClip)_selectedClip);
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
                AddAudioTrack();
            }

            UpdateTimelineHeight();
        }

        public void AddVideoTrack()
        {
            int trackIndex = _timelineController.Timeline.VideoTracks.Count;
            string trackName = $"V{trackIndex + 1}";

            VideoTrack track = new VideoTrack(trackName, _timelineController.Timeline.Fps);
            _timelineController.Timeline.VideoTracks.Insert(0, track);

            // Create track label with eye icon
            Grid labelGrid = new Grid();
            labelGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });
            labelGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            Border eyeButton = new Border
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

            TextBlock trackNameText = new TextBlock
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

            Border labelBorder = new Border
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

        public void AddAudioTrack()
        {
            int trackIndex = _timelineController.Timeline.AudioTracks.Count;
            string trackName = $"A{trackIndex + 1}";

            AudioTrack track = new AudioTrack(trackName);
            _timelineController.Timeline.AudioTracks.Add(track);

            Grid labelGrid = new Grid();
            labelGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });
            labelGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            Border muteButton = new Border
            {
                Width = 20,
                Height = 20,
                Background = Brushes.Transparent,
                Child = new TextBlock
                {
                    Text = "🔊",
                    FontSize = 14,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                },
                Cursor = System.Windows.Input.Cursors.Hand
            };
            Grid.SetColumn(muteButton, 0);

            TextBlock trackNameText = new TextBlock
            {
                Text = trackName,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                FontWeight = FontWeights.Bold
            };
            Grid.SetColumn(trackNameText, 1);

            labelGrid.Children.Add(muteButton);
            labelGrid.Children.Add(trackNameText);

            Border labelBorder = new Border
            {
                Height = TrackHeight,
                Background = new SolidColorBrush(Color.FromRgb(45, 45, 48)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(63, 63, 70)),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Child = labelGrid
            };

            _trackLabelsPanel.Children.Add(labelBorder);

            UpdateTimelineHeight();
            UpdateClipPositions();
        }

        private void UpdateTimelineHeight()
        {
            int trackCount = _timelineController.Timeline.VideoTracks.Count + _timelineController.Timeline.AudioTracks.Count;
            double totalHeight = trackCount * (TrackHeight);

            _timelineCanvas.Height = totalHeight;
            _trackLabelsPanel.Height = totalHeight;
        }

        public void AddVideoClipAtPosition(VideoSource source, double xPosition, double yPosition)
        {
            int trackIndex = GetVideoTrackIndexFromY(yPosition);
            if (trackIndex < 0 || trackIndex >= _timelineController.Timeline.VideoTracks.Count)
                return;

            long startFrame = _timelineController.PositionToFrame(xPosition);

            VideoClip clip = new VideoClip(
                source.FilePath,
                sourceStart: 0,
                timelineStart: startFrame,
                source: source);

            _timelineController.Timeline.VideoTracks[trackIndex].AddClip(clip);
            UpdateClipPositions();
        }

        public void AddAudioClipAtPosition(AudioSource source, double xPosition, double yPosition)
        {
            int trackIndex = GetAudioTrackIndexFromY(yPosition);
            if (trackIndex < 0 || trackIndex >= _timelineController.Timeline.AudioTracks.Count)
                return;
            long startFrame = _timelineController.PositionToFrame(xPosition);
            AudioClip clip = new AudioClip(
                source.FilePath,
                sourceStart: 0,
                timelineStart: startFrame,
                source: source);
            _timelineController.Timeline.AudioTracks[trackIndex].AddClip(clip);
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
            int newVideoTrackIndex = GetVideoTrackIndexFromY(position.Y);
            int newAudioTrackIndex = GetAudioTrackIndexFromY(position.Y);
            if (_selectedClip is VideoClip && newVideoTrackIndex >= 0 && newVideoTrackIndex < _timelineController.Timeline.VideoTracks.Count && newVideoTrackIndex != _selectedTrackIndex)
            {
                _timelineController.Timeline.VideoTracks[_selectedTrackIndex].RemoveClip((VideoClip)_selectedClip);
                _timelineController.Timeline.VideoTracks[newVideoTrackIndex].AddClip((VideoClip)_selectedClip);
                _selectedTrackIndex = newVideoTrackIndex;
            }
            else if(_selectedClip is AudioClip && newAudioTrackIndex >= 0 && newAudioTrackIndex < _timelineController.Timeline.AudioTracks.Count && newAudioTrackIndex != _selectedTrackIndex)
            {
                _timelineController.Timeline.AudioTracks[_selectedTrackIndex].RemoveClip((AudioClip)_selectedClip);
                _timelineController.Timeline.AudioTracks[newVideoTrackIndex].AddClip((AudioClip)_selectedClip);
                _selectedTrackIndex = newVideoTrackIndex;
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
                VideoTrack track = _timelineController.Timeline.VideoTracks[trackIndex];
                foreach (VideoClip clip in track.Clips)
                {
                    DrawVideoClip(clip, trackIndex);
                }
            }
            for (int trackIndex = 0; trackIndex < _timelineController.Timeline.AudioTracks.Count; trackIndex++)
            {
                AudioTrack track = _timelineController.Timeline.AudioTracks[trackIndex];
                foreach (AudioClip clip in track.Clips)
                {
                    DrawAudioClip(clip, trackIndex);
                }
            }

            HighlightSelectedClip();
        }

        public void SelectClipAtPosition(Point position)
        {
            int videoTrackIndex = GetVideoTrackIndexFromY(position.Y);
            int audioTrackIndex = GetAudioTrackIndexFromY(position.Y);
            if (!(audioTrackIndex >= 0 && audioTrackIndex < _timelineController.Timeline.AudioTracks.Count) && !(videoTrackIndex >= 0 && videoTrackIndex <= _timelineController.Timeline.VideoTracks.Count))
            {
                _selectedClip = null;
                _selectedTrackIndex = -1;
                ClearHighlights();
                return;
            }

            if(videoTrackIndex >= 0 && videoTrackIndex < _timelineController.Timeline.VideoTracks.Count)
            {
                long totalFrames = _timelineController.GetTotalFramesWithBuffer();
                VideoTrack track = _timelineController.Timeline.VideoTracks[videoTrackIndex];

                foreach (VideoClip clip in track.Clips)
                {
                    double clipX = _timelineController.FrameToPosition(clip.timelineStart);
                    double clipWidth = (clip.length * (_timelineController.Timeline.Fps / clip.source.FPS) / (double)totalFrames) * _timelineCanvas.Width;

                    if (position.X >= clipX && position.X <= clipX + clipWidth)
                    {
                        _selectedClip = clip;
                        _xTextBox.IsEnabled = true;
                        _xTextBox.Text = clip.x.ToString();
                        _yTextBox.IsEnabled = true;
                        _yTextBox.Text = clip.y.ToString();
                        _scaleTextBox.IsEnabled = true;
                        _scaleTextBox.Text = clip.scale.ToString("F2");
                        _opacityTextBox.IsEnabled = true;
                        _opacityTextBox.Text = clip.opacity.ToString("F2");
                        _selectedTrackIndex = videoTrackIndex;
                        ClipSelected?.Invoke(clip);
                        Debug.WriteLine($"Selected clip: {clip.name} on track {videoTrackIndex}");
                        HighlightSelectedClip();
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
            else if(audioTrackIndex >=0 && audioTrackIndex < _timelineController.Timeline.AudioTracks.Count)
            {
                long totalFrames = _timelineController.GetTotalFramesWithBuffer();
                AudioTrack track = _timelineController.Timeline.AudioTracks[audioTrackIndex];

                foreach (AudioClip clip in track.Clips)
                {
                    double clipX = _timelineController.FrameToPosition(clip.timelineStart);
                    long frameDuration = (long)(clip.Duration * _timelineController.Timeline.Fps);
                    double clipWidth = ((double)frameDuration / totalFrames) * _timelineCanvas.Width;

                    if (position.X >= clipX && position.X <= clipX + clipWidth)
                    {
                        _xTextBox.IsEnabled = false;
                        _yTextBox.IsEnabled = false;
                        _scaleTextBox.IsEnabled = false;
                        _opacityTextBox.IsEnabled = false;
                        _selectedClip = clip;
                        _selectedTrackIndex = audioTrackIndex;
                        ClipSelected?.Invoke(clip);
                        Debug.WriteLine($"Selected clip: {clip.name} on track {audioTrackIndex}");
                        HighlightSelectedClip();
                        return;
                    }
                }

                _selectedClip = null;
                _selectedTrackIndex = -1;
                ClearHighlights();
            }

            
        }

        private void CutClipAtPosition(Point position)
        {
            int videoTrackIndex = GetVideoTrackIndexFromY(position.Y);
            int audioTrackIndex = GetAudioTrackIndexFromY(position.Y);

            if(videoTrackIndex >= 0 && videoTrackIndex < _timelineController.Timeline.VideoTracks.Count)
            {
                long totalFrames = _timelineController.GetTotalFramesWithBuffer();
                VideoTrack track = _timelineController.Timeline.VideoTracks[videoTrackIndex];

                Debug.WriteLine($"Attempting to cut at frame: {_timelineController.PositionToFrame(position.X)}");

                foreach (VideoClip clip in track.Clips.ToList())
                {
                    double clipX = _timelineController.FrameToPosition(clip.timelineStart);
                    double clipWidth = (clip.length * (_timelineController.Timeline.Fps / clip.source.FPS) / (double)totalFrames) * _timelineCanvas.Width;

                    if (position.X >= clipX && position.X <= clipX + clipWidth)
                    {
                        long timelineCutFrame = _timelineController.PositionToFrame(position.X);
                        long cutFrame = (long)((timelineCutFrame - clip.timelineStart) * (clip.source.FPS / _timelineController.Timeline.Fps));
                        var(firstPart, secondPart) = clip.SplitAtFrame(cutFrame, _timelineController.Timeline);

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
            else if(audioTrackIndex >= 0 && audioTrackIndex < _timelineController.Timeline.AudioTracks.Count)
            {
                long totalFrames = _timelineController.GetTotalFramesWithBuffer();
                AudioTrack track = _timelineController.Timeline.AudioTracks[audioTrackIndex];

                Debug.WriteLine($"Attempting to cut at frame: {_timelineController.PositionToFrame(position.X)}");

                foreach (AudioClip clip in track.Clips.ToList())
                {
                    double clipX = _timelineController.FrameToPosition(clip.timelineStart);
                    long frameDuration = (long)(clip.Duration * _timelineController.Timeline.Fps);
                    double clipWidth = ((double)frameDuration / totalFrames) * _timelineCanvas.Width;

                    if (position.X >= clipX && position.X <= clipX + clipWidth)
                    {
                        long timelineCutFrame = _timelineController.PositionToFrame(position.X);
                        double timelineCutTime = (double)timelineCutFrame / _timelineController.Timeline.Fps;
                        var (firstPart, secondPart) = clip.SplitAtTime(timelineCutTime);

                        if (firstPart == null || secondPart == null)
                        {
                            Debug.WriteLine($"Cut failed: Invalid split at time {timelineCutTime} for clip {clip.name}");
                            return;
                        }

                        track.RemoveClip(clip);
                        track.AddClip(firstPart);
                        track.AddClip(secondPart);
                        UpdateClipPositions();
                        Debug.WriteLine($"Cut Audio Clip: {clip.name} at time {timelineCutTime}");
                        return;
                    }
                }
            }

            
        }

        private int GetVideoTrackIndexFromY(double y)
        {
            return (int)(y / TrackHeight);
        }

        private int GetAudioTrackIndexFromY(double y)
        {
            return (int)(y / TrackHeight) - _timelineController.Timeline.VideoTracks.Count;
        }

        private double GetVideoTrackY(int trackIndex)
        {
            return trackIndex * (TrackHeight);
        }

        private double GetAudioTrackY(int trackIndex)
        {
            return (_timelineController.Timeline.VideoTracks.Count + trackIndex) * (TrackHeight);
        }

        private void DrawVideoClip(VideoClip clip, int trackIndex)
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

            Border rect = new Border
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
            Canvas.SetTop(rect, GetVideoTrackY(trackIndex));

            clip.rect = rect;
            _timelineCanvas.Children.Add(rect);
        }

        private void DrawAudioClip(AudioClip clip, int trackIndex)
        {
            long totalFrames = _timelineController.GetTotalFramesWithBuffer();
            double clipX = _timelineController.FrameToPosition(clip.timelineStart);
            long frameDuration = (long)(clip.Duration * _timelineController.Timeline.Fps);
            double clipWidth = ((double)frameDuration / totalFrames) * _timelineCanvas.Width;
            Color[] trackColors = new[]
            {
                Color.FromRgb(100, 100, 100),
                Color.FromRgb(150, 150, 150),
                Color.FromRgb(200, 200, 200),
                Color.FromRgb(50, 50, 50)
            };
            Border rect = new Border
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
            Canvas.SetTop(rect, GetAudioTrackY(trackIndex));
            _timelineCanvas.Children.Add(rect);
        }

        private void HighlightSelectedClip()
        {
            if (_selectedClip?.rect != null)
            {   
                ClearHighlights();
                _selectedClip.rect.BorderBrush = Brushes.Red;
            }
            else
            {
                Debug.WriteLine("No rect?...");
            }
        }

        private void ClearHighlights()
        {
            foreach (Border border in _timelineCanvas.Children.OfType<Border>())
            {
                border.BorderBrush = Brushes.Black;
            }
        }

        private void ClearClips()
        {
            List<UIElement> toRemove = _timelineCanvas.Children
                .OfType<UIElement>()
                .Where(e => e.GetType() == typeof(Border))
                .ToList();

            foreach (UIElement element in toRemove)
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
            if (_selectedClip is VideoClip videoClip)
            {
                videoClip.x = x;
            }
        }

        public void UpdateSelectedClipY(int y)
        {
            if (_selectedClip is VideoClip videoClip)
            {
                videoClip.y = y;
            }
        }

        public void UpdateSelectedClipScale(double scale)
        {
            if (_selectedClip is VideoClip videoClip)
            {
                videoClip.scale = scale;
            }
        }

        public void UpdateSelectedClipOpacity(double opacity)
        {
            if (_selectedClip is VideoClip videoClip)
            {
                videoClip.opacity = opacity;
            }
        }
    }
}