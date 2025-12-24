using Microsoft.Win32;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using VESCO.Timeline;

namespace VESCO
{
    public partial class MainWindow : Window
    {
        private double FPS = 30;
        private double _currentTime; // seconds
        private bool _isDraggingPlayhead = false;
        private Timeline.Timeline timeline;
        private double timeLineDurationBuffer = 10*60; //10 minutes buffer

        public MainWindow()
        {
            InitializeComponent();
            timeline = new Timeline.Timeline(FPS);
        }

        protected override async void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (timeline.getTotalDuration()<=0)
                return;

            if (timeline.fps <= 0)
                return;

            double frameStep = 1.0 / timeline.fps;

            if (e.Key == Key.OemPeriod)
            {
                _currentTime = Math.Min(timeline.getTotalDuration() + timeLineDurationBuffer, _currentTime + frameStep);
                previewImage.Source = await timeline.getFrameAt(_currentTime);
                UpdatePlayheadFromTime();
                e.Handled = true;
            }
            else if (e.Key == Key.OemComma)
            {
                _currentTime = Math.Max(0, _currentTime - frameStep);
                previewImage.Source = await timeline.getFrameAt(_currentTime);
                UpdatePlayheadFromTime();
                e.Handled = true;
            }
            else if (e.Key == Key.OemPlus)
            {
                TimelineArea.Width += 100;
                UpdateClipPositions();
            }
            else if (e.Key == Key.OemMinus)
            {
                TimelineArea.Width -= 100;
                UpdateClipPositions();
            }
        }

        private void UpdatePlayheadFromTime()
        {
            if (timeline.getTotalDuration() <= 0)
                return;

            double x = (_currentTime / (timeline.getTotalDuration()+timeLineDurationBuffer)) * TimelineArea.ActualWidth;
            Canvas.SetLeft(Playhead, x);
        }

        private async void OpenVideo_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog()
            {
                Filter = "Video Files|*.mp4;*.avi;*.mov;*.mkv"
            };

            if (dialog.ShowDialog() != true)
                return;

            var _videoPath = dialog.FileName;

            var info = await Xabe.FFmpeg.FFmpeg.GetMediaInfo(_videoPath);
            var videoStream = info.VideoStreams.FirstOrDefault();
            if (videoStream == null)
            {
                MessageBox.Show("No video stream found!");
                return;
            }

            var fps = videoStream.Framerate;

            long frameCount = 0;
            var psi = new ProcessStartInfo
            {
                FileName = "ffprobe",
                Arguments = $"-v error -select_streams v:0 -count_frames -show_entries stream=nb_read_frames -print_format csv \"{_videoPath}\"",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (var process = Process.Start(psi))
            {
                string output = await process.StandardOutput.ReadToEndAsync();
                process.WaitForExit();

                var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    Debug.WriteLine($"ffprobe output line: {line}");
                    if (line.StartsWith("stream"))
                    {
                        var parts = line.Split(',');
                        if (parts.Length == 2 && long.TryParse(parts[1], out var n))
                        {
                            frameCount = n;
                            break;
                        }
                    }
                }
            }

            if (frameCount == 0)
            {
                MessageBox.Show("Failed to get frame count!");
                return;
            }

            var videoDuration = frameCount / fps;

            var timeLineStart = GetSnapTimeForNewClip(timeline.VideoTracks[0]);

            SourceMedia sourceMedia = new SourceMedia(_videoPath, frameCount, videoDuration);
            timeline.VideoTracks[0].AddClip(new VideoClip(sourceMedia.FilePath, 0, timeLineStart, sourceMedia));

            _currentTime = 0;

            Debug.WriteLine($"Loaded video: {_videoPath}");
            Debug.WriteLine($"FPS: {fps}, Frame count: {frameCount}, Duration: {videoDuration}s, Timeline start: {timeLineStart}");

            var firstFrame = await timeline.getFrameAt(0);
            previewImage.Source = firstFrame;

            UpdateClipPositions();
        }


        private double GetSnapTimeForNewClip(VideoTrack track)
        {
            if (track.Clips.Count == 0)
                return 0;

            var lastClip = track.Clips
                .OrderBy(c => c.TimelineStart)
                .Last();

            return lastClip.TimelineStart + lastClip.Source.Length;
        }


        private async void TimelineClick(object sender, MouseButtonEventArgs e)
        {
            _isDraggingPlayhead = true;
            Playhead.CaptureMouse();
            await UpdateFrameFromMouseAsync(e);
        }

        private async void TimelineMove(object sender, MouseEventArgs e)
        {
            if (_isDraggingPlayhead)
            {
                await UpdateFrameFromMouseAsync(e);
            }
        }

        private async void TimelineRelease(object sender, MouseButtonEventArgs e)
        {
            _isDraggingPlayhead = false;
            Playhead.ReleaseMouseCapture();
            await UpdateFrameFromMouseAsync(e);
        }

        private async Task UpdateFrameFromMouseAsync(MouseEventArgs e)
        {
            if (timeline.getTotalDuration() <= 0) return;

            double clickX = e.GetPosition(TimelineArea).X;
            clickX = Math.Max(0, Math.Min(TimelineArea.ActualWidth, clickX));

            Canvas.SetLeft(Playhead, clickX);

            double time = (clickX / TimelineArea.ActualWidth) * (timeline.getTotalDuration() + timeLineDurationBuffer);
            _currentTime = time;

            var frame = await timeline.getFrameAt(time);
            previewImage.Source = frame;
        }

        private void DrawVideoClip(VideoClip clip)
        {
            double clipX = clip.TimelineStart / (timeline.getTotalDuration() + timeLineDurationBuffer) * TimelineArea.ActualWidth;
            double clipWidth = clip.Source.Length / (timeline.getTotalDuration() + timeLineDurationBuffer) * TimelineArea.ActualWidth;
            double clipHeight = 60;


            var rect = new Border
            {
                Width = clipWidth,
                Height = clipHeight,
                Background = new SolidColorBrush(Color.FromRgb(70, 130, 180)), // steel blue
                BorderBrush = Brushes.Black,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(3)
            };

            Canvas.SetLeft(rect, clipX);
            Canvas.SetTop(rect, 10);

            var text = new TextBlock
            {
                Text = clip.Name,
                Foreground = Brushes.White,
                Margin = new Thickness(4, 2, 0, 0)
            };

            rect.Child = text;


            TimelineArea.Children.Add(rect);

            Debug.WriteLine($"Drawing clip at X={clipX}, W={clipWidth}");


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
            for (int i = 0; i < timeline.VideoTracks[0].Clips.Count; i++)
            {
                DrawVideoClip(timeline.VideoTracks[0].Clips[i]);
            }
        }
    }


}
