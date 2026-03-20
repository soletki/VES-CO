using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using VESCO.Timeline;

namespace VESCO.Managers
{
    public class ClipDrawManager
    {
        private readonly TimelineController _timelineController;
        private readonly Canvas _timelineCanvas;
        private int _trackHeight = 40;
        private static readonly Color[] VideoTrackColors =
        [
            Color.FromRgb(70, 130, 180),
            Color.FromRgb(180, 130, 70),
            Color.FromRgb(130, 180, 70),
            Color.FromRgb(180, 70, 130)
        ];
        private static readonly Color[] AudioTrackColors =
        [
            Color.FromRgb(100, 100, 100),
            Color.FromRgb(150, 150, 150),
            Color.FromRgb(200, 200, 200),
            Color.FromRgb(50, 50, 50)
        ];

        public ClipDrawManager(TimelineController timelineController, Canvas timelineCanvas)
        {
            _timelineController = timelineController;
            _timelineCanvas = timelineCanvas;
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
        }

        private void DrawVideoClip(VideoClip clip, int trackIndex)
        {
            double clipX = _timelineController.FrameToPosition(clip.TimelineStart);
            double clipWidth = GetVideoClipWidth(clip);
            Border rect = CreateClipBorder(clip.Name, VideoTrackColors[trackIndex % VideoTrackColors.Length], clipWidth);

            Canvas.SetLeft(rect, clipX);
            Canvas.SetTop(rect, GetVideoTrackY(trackIndex));

            clip.Rect = rect;
            _timelineCanvas.Children.Add(rect);
        }

        private void DrawAudioClip(AudioClip clip, int trackIndex)
        {
            double clipX = _timelineController.FrameToPosition(clip.TimelineStart);
            double clipWidth = GetAudioClipWidth(clip);
            Border rect = CreateClipBorder(clip.Name, AudioTrackColors[trackIndex % AudioTrackColors.Length], clipWidth);
            clip.Rect = rect;
            Canvas.SetLeft(rect, clipX);
            Canvas.SetTop(rect, GetAudioTrackY(trackIndex));
            _timelineCanvas.Children.Add(rect);
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

        private double GetVideoTrackY(int trackIndex)
        {
            return trackIndex * _trackHeight;
        }

        private double GetAudioTrackY(int trackIndex)
        {
            return (_timelineController.Timeline.VideoTracks.Count + trackIndex) * _trackHeight;
        }

        public void HighlightClip(Clip? clip)
        {
            if (clip != null && clip?.Rect != null)
            {
                ClearHighlights();
                clip.Rect.BorderBrush = Brushes.Red;
            }
        }

        public void SetTrackHeight(int trackHeight)
        {
            _trackHeight = Math.Max(10, trackHeight);
        }

        public void ClearHighlights()
        {
            foreach (Border border in _timelineCanvas.Children.OfType<Border>())
            {
                border.BorderBrush = Brushes.Black;
            }
        }

        private Border CreateClipBorder(string clipName, Color color, double clipWidth)
        {
            return new Border
            {
                Width = Math.Max(4, clipWidth),
                Height = _trackHeight,
                Background = new SolidColorBrush(color),
                BorderBrush = Brushes.Black,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(3),
                Child = new TextBlock
                {
                    Text = clipName,
                    Foreground = Brushes.White,
                    Margin = new Thickness(4, 2, 0, 0)
                }
            };
        }

        private double GetVideoClipWidth(VideoClip clip)
        {
            long totalFrames = _timelineController.GetTotalFramesWithBuffer();
            return clip.Length * (_timelineController.Timeline.Fps / clip.Source.FPS) / totalFrames * _timelineCanvas.Width;
        }

        private double GetAudioClipWidth(AudioClip clip)
        {
            long totalFrames = _timelineController.GetTotalFramesWithBuffer();
            long frameDuration = (long)(clip.Duration * _timelineController.Timeline.Fps);
            return (double)frameDuration / totalFrames * _timelineCanvas.Width;
        }
    }
}
