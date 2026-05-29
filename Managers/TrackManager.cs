using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Extensions.Logging;
using VESCO.Timeline;

namespace VESCO.Managers
{
    public class TrackManager
    {
        private readonly TimelineController _timelineController;
        private readonly Canvas _timelineCanvas;
        private readonly StackPanel _trackLabelsPanel;
        private readonly ClipDrawManager _clipDrawManager;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger<TrackManager> _logger;

        private int _trackHeight = 40;

        public TrackManager(
            TimelineController timelineController,
            Canvas timelineCanvas,
            StackPanel trackLabelsPanel,
            ClipDrawManager clipDrawManager,
            ILoggerFactory loggerFactory)
        {
            _timelineController = timelineController;
            _timelineCanvas = timelineCanvas;
            _trackLabelsPanel = trackLabelsPanel;
            _clipDrawManager = clipDrawManager;
            _loggerFactory = loggerFactory;
            _logger = loggerFactory.CreateLogger<TrackManager>();
            _clipDrawManager.SetTrackHeight(_trackHeight);
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
            _logger.LogDebug("Increased track height to {TrackHeight}", _trackHeight);
            _clipDrawManager.SetTrackHeight(_trackHeight);
            UpdateTimelineHeight();
            _clipDrawManager.UpdateClipPositions();
            UpdateLabelsHeight();
        }

        public void DecreaseTrackHeight()
        {
            _trackHeight = Math.Max(10, _trackHeight - 10);
            _logger.LogDebug("Decreased track height to {TrackHeight}", _trackHeight);
            _clipDrawManager.SetTrackHeight(_trackHeight);
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

        private void UpdateLabels()
        {
            _trackLabelsPanel.Children.Clear();
            for (int i = 0; i < _timelineController.Timeline.VideoTracks.Count; i++)
            {
                _trackLabelsPanel.Children.Insert(
                    0,
                    CreateTrackLabel(_timelineController.Timeline.VideoTracks[i].Name, "👁"));
            }

            for(int i=0; i< _timelineController.Timeline.AudioTracks.Count; i++)
            {
                _trackLabelsPanel.Children.Add(
                    CreateTrackLabel(_timelineController.Timeline.AudioTracks[i].Name, "🔊"));
            }
        }

        public void AddVideoTrack()
        {
            int trackIndex = _timelineController.Timeline.VideoTracks.Count;
            string trackName = $"V{trackIndex + 1}";

            VideoTrack track = new VideoTrack(trackName, _timelineController.Timeline.Fps);
            _timelineController.Timeline.VideoTracks.Insert(0, track);
            _logger.LogInformation("Added video track {TrackName}", trackName);

            RefreshTimelineLayout();
        }

        public void AddAudioTrack()
        {
            int trackIndex = _timelineController.Timeline.AudioTracks.Count;
            string trackName = $"A{trackIndex + 1}";

            AudioTrack track = new AudioTrack(trackName);
            _timelineController.Timeline.AudioTracks.Add(track);
            _logger.LogInformation("Added audio track {TrackName}", trackName);

            RefreshTimelineLayout();
        }

        public void AddVideoClipAtPosition(VideoSource source, double xPosition, double yPosition)
        {
            int trackIndex = GetVideoTrackIndexFromY(yPosition);
            if (trackIndex < 0 || trackIndex >= _timelineController.Timeline.VideoTracks.Count)
            {
                _logger.LogWarning("Ignored video clip drop for {FilePath} because track index {TrackIndex} is invalid", source.FilePath, trackIndex);
                return;
            }

            long startFrame = _timelineController.PositionToFrame(xPosition);

            VideoClip clip = new VideoClip(
                source.FilePath,
                sourceStart: 0,
                timelineStart: startFrame,
                source: source,
                timelineFps: _timelineController.Timeline.Fps,
                logger: _loggerFactory.CreateLogger<VideoClip>());

            _timelineController.Timeline.VideoTracks[trackIndex].AddClip(clip);
            _logger.LogInformation("Added video clip {ClipName} to track {TrackIndex} at frame {StartFrame}", clip.Name, trackIndex, startFrame);
            _clipDrawManager.UpdateClipPositions();
        }

        public void AddAudioClipAtPosition(AudioSource source, double xPosition, double yPosition)
        {
            int trackIndex = GetAudioTrackIndexFromY(yPosition);
            if (trackIndex < 0 || trackIndex >= _timelineController.Timeline.AudioTracks.Count)
            {
                _logger.LogWarning("Ignored audio clip drop for {FilePath} because track index {TrackIndex} is invalid", source.FilePath, trackIndex);
                return;
            }
            long startFrame = _timelineController.PositionToFrame(xPosition);
            AudioClip clip = new AudioClip(
                source.FilePath,
                sourceStart: 0,
                timelineStart: startFrame,
                source: source);
            _timelineController.Timeline.AudioTracks[trackIndex].AddClip(clip);
            _logger.LogInformation("Added audio clip {ClipName} to track {TrackIndex} at frame {StartFrame}", clip.Name, trackIndex, startFrame);
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

        public void ClearTracks()
        {
            _logger.LogInformation("Clearing all timeline tracks");
            for(int i=0;i< _timelineController.Timeline.VideoTracks.Count; i++) { 
                for(int j=0;j< _timelineController.Timeline.VideoTracks[i].Clips.Count; j++)
                {
                    _timelineController.Timeline.VideoTracks[i].Clips[j].Dispose();
                }
            }

            _timelineController.Timeline.VideoTracks.Clear();
            _timelineController.Timeline.AudioTracks.Clear();
            InitializeTracks();
            RefreshTimelineLayout();
        }

        private void RefreshTimelineLayout()
        {
            _clipDrawManager.SetTrackHeight(_trackHeight);
            UpdateLabels();
            UpdateTimelineHeight();
            _clipDrawManager.UpdateClipPositions();
        }

        private Border CreateTrackLabel(string trackName, string iconText)
        {
            Grid labelGrid = new Grid();
            labelGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });
            labelGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            Border iconButton = new Border
            {
                Width = 20,
                Height = 20,
                Background = Brushes.Transparent,
                Child = new TextBlock
                {
                    Text = iconText,
                    FontSize = 14,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                },
                Cursor = System.Windows.Input.Cursors.Hand
            };
            Grid.SetColumn(iconButton, 0);

            TextBlock trackNameText = new TextBlock
            {
                Text = trackName,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                FontWeight = FontWeights.Bold
            };
            Grid.SetColumn(trackNameText, 1);

            labelGrid.Children.Add(iconButton);
            labelGrid.Children.Add(trackNameText);

            return new Border
            {
                Height = _trackHeight,
                Background = new SolidColorBrush(Color.FromRgb(45, 45, 48)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(63, 63, 70)),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Child = labelGrid
            };
        }
    }
}
