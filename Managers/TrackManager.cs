using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using VESCO.Timeline;

namespace VESCO.Managers
{
    public class TrackManager
    {

        private readonly TimelineController _timelineController;
        private readonly Canvas _timelineCanvas;
        private readonly StackPanel _trackLabelsPanel;
        private readonly ClipDrawManager _clipDrawManager;

        private int _trackHeight = 40;

        public TrackManager(
            TimelineController timelineController,
            Canvas timelineCanvas,
            StackPanel trackLabelsPanel,
            ClipDrawManager clipDrawManager)
        {
            _timelineController = timelineController;
            _timelineCanvas = timelineCanvas;
            _trackLabelsPanel = trackLabelsPanel;
            _clipDrawManager = clipDrawManager;
        }

        private void UpdateTimelineHeight()
        {
            int trackCount = _timelineController.Timeline.VideoTracks.Count + _timelineController.Timeline.AudioTracks.Count;
            double totalHeight = trackCount * _trackHeight;

            _timelineCanvas.Height = totalHeight;
            _trackLabelsPanel.Height = totalHeight;
        }

        public void IncreaseTrackHeight()
        {
            _trackHeight += 10;

            UpdateTimelineHeight();
            _clipDrawManager.UpdateClipPositions();
            UpdateLabelsHeight();
        }

        public void DecreaseTrackHeight()
        {
            _trackHeight = Math.Max(10, _trackHeight - 10);

            UpdateTimelineHeight();
            _clipDrawManager.UpdateClipPositions();
            UpdateLabelsHeight();
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

        private void UpdateLabelsHeight()
        {
            for (int i = 0; i < _trackLabelsPanel.Children.Count; i++)
            {
                if (_trackLabelsPanel.Children[i] is Border border)
                {
                    border.Height = _trackHeight;
                }
            }
        }

        public void AddVideoTrack()
        {
            int trackIndex = _timelineController.Timeline.VideoTracks.Count;
            string trackName = $"V{trackIndex + 1}";

            VideoTrack track = new VideoTrack(trackName, _timelineController.Timeline.Fps);
            _timelineController.Timeline.VideoTracks.Insert(0, track);

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
                Height = _trackHeight,
                Background = new SolidColorBrush(Color.FromRgb(45, 45, 48)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(63, 63, 70)),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Child = labelGrid
            };

            _trackLabelsPanel.Children.Insert(0, labelBorder);

            UpdateTimelineHeight();
            _clipDrawManager.UpdateClipPositions();
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
                Height = _trackHeight,
                Background = new SolidColorBrush(Color.FromRgb(45, 45, 48)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(63, 63, 70)),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Child = labelGrid
            };

            _trackLabelsPanel.Children.Add(labelBorder);

            UpdateTimelineHeight();
            _clipDrawManager.UpdateClipPositions();
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
            _clipDrawManager.UpdateClipPositions();
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
            _clipDrawManager.UpdateClipPositions();
        }

        public int GetVideoTrackIndexFromY(double y)
        {
            return (int)(y / _trackHeight);
        }

        public int GetAudioTrackIndexFromY(double y)
        {
            return (int)(y / _trackHeight) - _timelineController.Timeline.VideoTracks.Count;
        }
    }
}
