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
            _isDraggingPlayhead = true;
            Playhead.CaptureMouse();
            UpdateFrameFromMouse(e);
            Debug.WriteLine($"Current frame: {_currentFrame}");
        }

        private void TimelineMove(object sender, MouseEventArgs e)
        {
            if (_isDraggingPlayhead)
                UpdateFrameFromMouse(e);
            Debug.WriteLine($"Current frame: {_currentFrame}");
        }

        private void TimelineRelease(object sender, MouseButtonEventArgs e)
        {
            _isDraggingPlayhead = false;
            Playhead.ReleaseMouseCapture();
            Debug.WriteLine($"Current frame: {_currentFrame}");
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
        }
    }


}
