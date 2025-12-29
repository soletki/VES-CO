using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using VESCO.Timeline;

namespace VESCO
{



    public partial class MainWindow : Window
    {
        private const double TimelineFPS = 60;
        private long _currentFrame;
        private bool _isDraggingPlayhead = false;
        private Timeline.Timeline timeline;
        private double timeLineDurationBuffer = 10*60; //10 minutes buffer
        private bool _isDraggingClip = false;
        private Point _clipDragStartMouse;
        private long _clipDragStartFrame;

        private enum Tool
        {
            None,
            Select
        }

        private Tool _activeTool = Tool.None;
        private VideoClip _selectedClip = null;


        public ObservableCollection<SourceMedia> MediaBin { get; } = new();

        public MainWindow()
        {
            InitializeComponent();
            timeline = new Timeline.Timeline(TimelineFPS);
            DataContext = this;
        }

        protected override async void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            long totalFrames = timeline.GetTotalFrames();
            long bufferFrames = (long)(timeLineDurationBuffer * TimelineFPS);

            if (totalFrames <= 0)
                return;

            double frameStep = 1.0 / timeline.Fps;

            if (e.Key == Key.OemPeriod)
            {
                _currentFrame = Math.Min(totalFrames + bufferFrames, _currentFrame + 1);
                UpdatePreviewFromFrame();
                Debug.WriteLine($"Current frame: {_currentFrame}");
                e.Handled = true;
            }
            else if (e.Key == Key.OemComma)
            {
                _currentFrame = Math.Max(0, _currentFrame - 1);
                UpdatePreviewFromFrame();
                Debug.WriteLine($"Current frame: {_currentFrame}");
                e.Handled = true;
            }
            else if (e.Key == Key.OemPlus)
            {
                TimelineArea.Width *= 1.2;
                UpdatePlayheadPosition();
                UpdateClipPositions();
            }
            else if (e.Key == Key.OemMinus)
            {
                TimelineArea.Width = Math.Max(200, TimelineArea.Width / 1.2);
                UpdatePlayheadPosition();
                UpdateClipPositions();
            }
        }

        private void FrameBackClick(object sender, RoutedEventArgs e)
        {
            _currentFrame = Math.Max(0, _currentFrame - 1);
            UpdatePlayheadPosition();
            UpdatePreviewFromFrame();
            Debug.WriteLine($"Current frame: {_currentFrame}");
        }

        private void FrameForwardClick(object sender, RoutedEventArgs e)
        {
            long totalFrames = timeline.GetTotalFrames();
            long bufferFrames = (long)(timeLineDurationBuffer * TimelineFPS);
            _currentFrame = Math.Min(totalFrames + bufferFrames, _currentFrame + 1);
            UpdatePlayheadPosition();
            UpdatePreviewFromFrame();
            Debug.WriteLine($"Current frame: {_currentFrame}");
        }

        private void UpdatePreviewFromFrame()
        {
            previewImage.Source = timeline.GetFrameAtFrame(_currentFrame);
        }

        private void OpenVideo_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Video Files|*.mp4;*.avi;*.mov;*.mkv"
            };

            if (dialog.ShowDialog() != true)
                return;

            var source = new SourceMedia(dialog.FileName);

            MediaBin.Add(source);
        }

        private void MediaBinMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (MediaBinList.SelectedItem is not SourceMedia source)
                return;

            Debug.WriteLine($"Selected item: {source.FileName}");

            DragDrop.DoDragDrop(
                MediaBinList,
                source,
                DragDropEffects.Copy);
        }

        private void TimelineDrop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(typeof(SourceMedia)))
                return;

            var source = (SourceMedia)e.Data.GetData(typeof(SourceMedia));

            double x = e.GetPosition(TimelineArea).X;
            x = Math.Clamp(x, 0, TimelineArea.Width);

            long totalFrames = timeline.GetTotalFrames() + (long)(timeLineDurationBuffer * timeline.Fps);
            long startFrame = (long)((x / TimelineArea.Width) * totalFrames);

            var clip = new VideoClip(
                source.FilePath,
                sourceStartFrame: 0,
                timelineStartFrame: startFrame,
                source: source);

            timeline.VideoTracks[0].AddClip(clip);

            UpdateClipPositions();

            UpdatePreviewFromFrame();
        }

        private void UpdatePlayheadPosition()
        {
            long totalFrames =
                timeline.GetTotalFrames() +
                (long)(timeLineDurationBuffer * TimelineFPS);
            double x =
                (_currentFrame / (double)totalFrames) * TimelineArea.Width;
            Canvas.SetLeft(Playhead, x);
        }

        private void TimelineClick(object sender, MouseButtonEventArgs e)
        {
            if (_activeTool == Tool.Select)
            {
                Point pos = e.GetPosition(TimelineArea);
                SelectClipAtPosition(pos);

                if (_selectedClip != null)
                {
                    _isDraggingClip = true;
                    _clipDragStartMouse = pos;
                    _clipDragStartFrame = _selectedClip.TimelineStart;
                    TimelineArea.CaptureMouse();
                }

                return;
            }

            _isDraggingPlayhead = true;
            Playhead.CaptureMouse();
            UpdateFrameFromMouse(e);
        }


        private void TimelineMove(object sender, MouseEventArgs e)
        {
            if (_isDraggingClip && _selectedClip != null)
            {
                Point pos = e.GetPosition(TimelineArea);
                double deltaX = pos.X - _clipDragStartMouse.X;

                long totalFrames = timeline.GetTotalFrames() + (long)(timeLineDurationBuffer * TimelineFPS);
                long deltaFrames = (long)((deltaX / TimelineArea.Width) * totalFrames);

                _selectedClip.TimelineStart = Math.Max(0, _clipDragStartFrame + deltaFrames);

                UpdateClipPositions();
                UpdatePreviewFromFrame();
            }
            else if (_isDraggingPlayhead)
            {
                UpdateFrameFromMouse(e);
            }

        }


        private void TimelineRelease(object sender, MouseButtonEventArgs e)
        {
            if (_isDraggingClip)
            {
                _isDraggingClip = false;
                TimelineArea.ReleaseMouseCapture();
                Debug.WriteLine($"Clip dropped: {_selectedClip?.Name}");
                return;
            }

            _isDraggingPlayhead = false;
            Playhead.ReleaseMouseCapture();
        }


        private void SelectClipAtPosition(Point position)
        {
            long totalFrames = timeline.GetTotalFrames() + (long)(timeLineDurationBuffer * TimelineFPS);

            foreach (var clip in timeline.VideoTracks[0].Clips)
            {
                double clipX = (clip.TimelineStart / (double)totalFrames) * TimelineArea.Width;
                double clipWidth = (clip.Length * (timeline.Fps / clip.Source.FPS) / (double)totalFrames) * TimelineArea.Width;

                if (position.X >= clipX && position.X <= clipX + clipWidth)
                {
                    _selectedClip = clip;
                    HighlightSelectedClip();
                    Debug.WriteLine($"Selected clip: {clip.Name}");
                    return;
                }
            }

            _selectedClip = null;
            ClearClipHighlights();
        }

        private void HighlightSelectedClip()
        {
            if(_selectedClip != null && _selectedClip.rect != null)
            {
                ClearClipHighlights();
                _selectedClip.rect.BorderBrush = Brushes.Red;
            }

            Debug.WriteLine($"Highlighted clip has no rect");
        }

        private void ClearClipHighlights()
        {
            foreach (var child in TimelineArea.Children.OfType<Border>())
                child.BorderBrush = Brushes.Black;
        }



        private void UpdateFrameFromMouse(MouseEventArgs e)
        {
            double x = e.GetPosition(TimelineArea).X;
            x = Math.Clamp(x, 0, TimelineArea.Width);
            long totalFrames =
                timeline.GetTotalFrames() +
                (long)(timeLineDurationBuffer * TimelineFPS);

            _currentFrame = (long)((x / TimelineArea.Width) * totalFrames);
            UpdatePlayheadPosition();
            UpdatePreviewFromFrame();
        }

        private void DrawVideoClip(VideoClip clip)
        {
            long totalFrames =
                timeline.GetTotalFrames() +
                (long)(timeLineDurationBuffer * TimelineFPS);

            double clipX =
                (clip.TimelineStart / (double)totalFrames) * TimelineArea.Width;

            double clipWidth =
                (clip.Length * (timeline.Fps/clip.Source.FPS) / (double)totalFrames) * TimelineArea.Width;

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
            Debug.WriteLine($"Set rect for clip");

            TimelineArea.Children.Add(rect);
        }

        private void ClearTimelineClips()
        {
            var toRemove = TimelineArea.Children
                .OfType<UIElement>()
                .Where(e => e != Playhead)
                .ToList();

            foreach (var element in toRemove)
            {
                TimelineArea.Children.Remove(element);
            }
        }

        private void UpdateClipPositions()
        {
            ClearTimelineClips();

            foreach (var clip in timeline.VideoTracks[0].Clips)
                DrawVideoClip(clip);

            HighlightSelectedClip();
        }

        private void SelectToolClick(object sender, RoutedEventArgs e)
        {
            if (_activeTool != Tool.Select)
            {
                _activeTool = Tool.Select;
                SelectTool.BorderBrush = Brushes.Blue;
            }
            else
            {
                _activeTool = Tool.None;
                SelectTool.BorderBrush = Brushes.Black;
            }
        }

    }


}
