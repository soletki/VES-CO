using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Xabe.FFmpeg;
using System.Linq;

namespace VESCO
{
    public partial class MainWindow : Window
    {
        private string _videoPath;
        private double _fps;
        private double _currentTime; // seconds
        private double _videoDuration; // seconds
        private bool _isDraggingPlayhead = false;

        public MainWindow()
        {
            InitializeComponent();
        }

        protected override async void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (string.IsNullOrEmpty(_videoPath))
                return;

            if (_fps <= 0)
                return;

            double frameStep = 1.0 / _fps;

            if (e.Key == Key.Right)
            {
                _currentTime = Math.Min(_videoDuration, _currentTime + frameStep);
                previewImage.Source = await GetFrameAtTimeAsync(_currentTime);
                UpdatePlayheadFromTime();
                e.Handled = true;
            }
            else if (e.Key == Key.Left)
            {
                _currentTime = Math.Max(0, _currentTime - frameStep);
                previewImage.Source = await GetFrameAtTimeAsync(_currentTime);
                UpdatePlayheadFromTime();
                e.Handled = true;
            }
        }

        private void UpdatePlayheadFromTime()
        {
            if (_videoDuration <= 0)
                return;

            double x = (_currentTime / _videoDuration) * TimelineArea.ActualWidth;
            Canvas.SetLeft(Playhead, x);
        }

        private async void OpenVideo_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog()
            {
                Filter = "Video Files|*.mp4;*.avi;*.mov;*.mkv"
            };

            if (dialog.ShowDialog() == true)
            {
                _videoPath = dialog.FileName;

                // Get video duration
                var info = await Xabe.FFmpeg.FFmpeg.GetMediaInfo(_videoPath);
                _videoDuration = info.Duration.TotalSeconds;

                var videoStream = info.VideoStreams.FirstOrDefault();
                _fps = videoStream.Framerate;

                _currentTime = 0;

                Debug.WriteLine($"Loaded video: {_videoPath}, Duration: {_videoDuration}s");

                // Show first frame
                var firstFrame = await GetFrameAtTimeAsync(0);
                previewImage.Source = firstFrame;
            }
        }

        /// <summary>
        /// Extract a single frame at a given timestamp
        /// </summary>
        private async Task<BitmapImage> GetFrameAtTimeAsync(double seconds)
        {
            if (string.IsNullOrEmpty(_videoPath))
                return null;

            string tempFrame = Path.Combine(Path.GetTempPath(), $"frame_{Guid.NewGuid()}.png");

            var conversion = Xabe.FFmpeg.FFmpeg.Conversions.New()
                .AddParameter($"-ss {seconds.ToString(System.Globalization.CultureInfo.InvariantCulture)}")
                .AddParameter($"-i \"{_videoPath}\"")
                .AddParameter("-vframes 1")
                .AddParameter($"\"{tempFrame}\"", ParameterPosition.PostInput);

            await conversion.Start();

            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.UriSource = new Uri(tempFrame);
            bmp.EndInit();

            try { File.Delete(tempFrame); } catch { }

            return bmp;
        }

        /// <summary>
        /// Handle clicking on the timeline
        /// </summary>
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

        /// <summary>
        /// Updates the playhead position and frame preview
        /// </summary>
        private async Task UpdateFrameFromMouseAsync(MouseEventArgs e)
        {
            if (_videoDuration <= 0) return;

            double clickX = e.GetPosition(TimelineArea).X;
            clickX = Math.Max(0, Math.Min(TimelineArea.ActualWidth, clickX));

            // Move playhead
            Canvas.SetLeft(Playhead, clickX);

            // Map click position to video timestamp
            double time = (clickX / TimelineArea.ActualWidth) * _videoDuration;
            _currentTime = time;

            // Load frame
            var frame = await GetFrameAtTimeAsync(time);
            previewImage.Source = frame;
        }
    }
}
