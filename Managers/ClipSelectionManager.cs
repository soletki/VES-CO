using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using VESCO.Timeline;

namespace VESCO.Managers
{
    public class ClipSelectionManager
    {

        private Clip ?_selectedClip;
        private int _selectedTrackIndex = -1;
        private TimelineController _timelineController;
        private ClipDrawManager _clipDrawManager;
        private TrackManager _trackManager;
        private bool _isDragging = false;
        private Point _dragStartMouse;
        private long _dragStartFrame;
        private Canvas _timelineCanvas;
        private readonly TextBox _xTextBox;
        private readonly Slider _xSlider;
        private readonly TextBox _yTextBox;
        private readonly Slider _ySlider;
        private readonly TextBox _scaleTextBox;
        private readonly Slider _scaleSlider;
        private readonly TextBox _opacityTextBox;
        private readonly Slider _opacitySlider;

        public bool IsDragging => _isDragging;

        public ClipSelectionManager
            (
                TimelineController timelineController,
                ClipDrawManager clipDrawManager,
                TrackManager trackManager,
                Canvas timelineCanvas,
                TextBox xTextBox,
                Slider xSlider,
                TextBox yTextBox,
                Slider ySlider,
                TextBox scaleTextBox,
                Slider scaleSlider,
                TextBox opacityTextBox,
                Slider opacitySlider
            )
        {
            _timelineController = timelineController;
            _clipDrawManager = clipDrawManager;
            _trackManager = trackManager;
            _timelineCanvas = timelineCanvas;
            _xTextBox = xTextBox;
            _xSlider = xSlider;
            _yTextBox = yTextBox;
            _ySlider = ySlider;
            _scaleTextBox = scaleTextBox;
            _scaleSlider = scaleSlider;
            _opacityTextBox = opacityTextBox;
            _opacitySlider = opacitySlider;
        }

        public void DeleteSelectedClip()
        {
            if (_selectedClip != null && _selectedTrackIndex >= 0)
            {
                if (_selectedClip is AudioClip)
                    _timelineController.Timeline.AudioTracks[_selectedTrackIndex].RemoveClip((AudioClip)_selectedClip);
                else
                    _timelineController.Timeline.VideoTracks[_selectedTrackIndex].RemoveClip((VideoClip)_selectedClip);
                _selectedClip = null;
                _selectedTrackIndex = -1;
                _clipDrawManager.UpdateClipPositions();
            }
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
            if (_selectedClip == null || _selectedTrackIndex < 0) return;

            double deltaX = position.X - _dragStartMouse.X;
            long totalFrames = _timelineController.GetTotalFramesWithBuffer();
            long deltaFrames = (long)(deltaX / _timelineCanvas.Width * totalFrames);

            _selectedClip.TimelineStart = Math.Max(0, _dragStartFrame + deltaFrames);

            int newVideoTrackIndex = _trackManager.GetVideoTrackIndexFromY(position.Y);
            int newAudioTrackIndex = _trackManager.GetAudioTrackIndexFromY(position.Y);
            if (_selectedClip is VideoClip && newVideoTrackIndex >= 0 && newVideoTrackIndex < _timelineController.Timeline.VideoTracks.Count && newVideoTrackIndex != _selectedTrackIndex)
            {
                
                _timelineController.Timeline.VideoTracks[_selectedTrackIndex].RemoveClip((VideoClip)_selectedClip);
                _timelineController.Timeline.VideoTracks[newVideoTrackIndex].AddClip((VideoClip)_selectedClip);
                _selectedTrackIndex = newVideoTrackIndex;
            }
            else if (_selectedClip is AudioClip && newAudioTrackIndex >= 0 && newAudioTrackIndex < _timelineController.Timeline.AudioTracks.Count && newAudioTrackIndex != _selectedTrackIndex)
            {
                _timelineController.Timeline.AudioTracks[_selectedTrackIndex].RemoveClip((AudioClip)_selectedClip);
                _timelineController.Timeline.AudioTracks[newAudioTrackIndex].AddClip((AudioClip)_selectedClip);
                _selectedTrackIndex = newAudioTrackIndex;
            }

            _clipDrawManager.UpdateClipPositions();
            _clipDrawManager.HighlightClip(_selectedClip);
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

        public void SelectClipAtPosition(Point position)
        {
            int videoTrackIndex = _trackManager.GetVideoTrackIndexFromY(position.Y);
            int audioTrackIndex = _trackManager.GetAudioTrackIndexFromY(position.Y);
            if (!(audioTrackIndex >= 0 && audioTrackIndex < _timelineController.Timeline.AudioTracks.Count) && !(videoTrackIndex >= 0 && videoTrackIndex <= _timelineController.Timeline.VideoTracks.Count))
            {
                _selectedClip = null;
                _selectedTrackIndex = -1;
                _clipDrawManager.ClearHighlights();
                return;
            }

            if (videoTrackIndex >= 0 && videoTrackIndex < _timelineController.Timeline.VideoTracks.Count)
            {
                long totalFrames = _timelineController.GetTotalFramesWithBuffer();
                VideoTrack track = _timelineController.Timeline.VideoTracks[videoTrackIndex];

                foreach (VideoClip clip in track.Clips)
                {
                    double clipX = _timelineController.FrameToPosition(clip.TimelineStart);
                    double clipWidth = clip.Length * (_timelineController.Timeline.Fps / clip.Source.FPS) / totalFrames * _timelineCanvas.Width;

                    if (position.X >= clipX && position.X <= clipX + clipWidth)
                    {
                        _selectedClip = clip;

                        _xTextBox.IsEnabled = true;
                        _xTextBox.Text = clip.X.ToString();

                        _xSlider.IsEnabled = true;
                        _xSlider.Value = clip.X;
                        _xSlider.Minimum = -clip.Source.Width;
                        _xSlider.Maximum = clip.Source.Width;

                        _yTextBox.IsEnabled = true;
                        _yTextBox.Text = clip.Y.ToString();
                        _ySlider.Minimum = -clip.Source.Height;
                        _ySlider.Maximum = clip.Source.Height;

                        _ySlider.IsEnabled = true;
                        _ySlider.Value = clip.Y;

                        _scaleTextBox.IsEnabled = true;
                        _scaleTextBox.Text = clip.Scale.ToString("F2");

                        _scaleSlider.IsEnabled = true;
                        _scaleSlider.Value = clip.Scale;

                        _opacityTextBox.IsEnabled = true;
                        _opacityTextBox.Text = clip.Opacity.ToString("F2");

                        _opacitySlider.IsEnabled = true;
                        _opacitySlider.Value = clip.Opacity;

                        _selectedTrackIndex = videoTrackIndex;
                        Debug.WriteLine($"Selected clip: {clip.Name} on track {videoTrackIndex}");
                        _clipDrawManager.HighlightClip(_selectedClip);
                        return;
                    }
                }

                _selectedClip = null;
                _xTextBox.IsEnabled = false;
                _xSlider.IsEnabled = false;
                _yTextBox.IsEnabled = false;
                _ySlider.IsEnabled = false;
                _scaleTextBox.IsEnabled = false;
                _scaleSlider.IsEnabled = false;
                _opacityTextBox.IsEnabled = false;
                _opacitySlider.IsEnabled = false;
                _selectedTrackIndex = -1;
                _clipDrawManager.ClearHighlights();
            }
            else if (audioTrackIndex >= 0 && audioTrackIndex < _timelineController.Timeline.AudioTracks.Count)
            {
                long totalFrames = _timelineController.GetTotalFramesWithBuffer();
                AudioTrack track = _timelineController.Timeline.AudioTracks[audioTrackIndex];

                foreach (AudioClip clip in track.Clips)
                {
                    double clipX = _timelineController.FrameToPosition(clip.TimelineStart);
                    long frameDuration = (long)(clip.Duration * _timelineController.Timeline.Fps);
                    double clipWidth = (double)frameDuration / totalFrames * _timelineCanvas.Width;

                    if (position.X >= clipX && position.X <= clipX + clipWidth)
                    {
                        _xTextBox.IsEnabled = false;
                        _xSlider.IsEnabled = false;
                        _yTextBox.IsEnabled = false;
                        _ySlider.IsEnabled = false;
                        _scaleTextBox.IsEnabled = false;
                        _scaleSlider.IsEnabled = false;
                        _opacityTextBox.IsEnabled = false;
                        _opacitySlider.IsEnabled = false;
                        _selectedClip = clip;
                        _selectedTrackIndex = audioTrackIndex;
                        Debug.WriteLine($"Selected clip: {clip.Name} on track {audioTrackIndex}");
                        _clipDrawManager.HighlightClip(_selectedClip);
                        return;
                    }
                }

                _selectedClip = null;
                _selectedTrackIndex = -1;
                _clipDrawManager.ClearHighlights();
            }


        }

        private void CutClipAtPosition(Point position)
        {
            int videoTrackIndex = _trackManager.GetVideoTrackIndexFromY(position.Y);
            int audioTrackIndex = _trackManager.GetAudioTrackIndexFromY(position.Y);

            if (videoTrackIndex >= 0 && videoTrackIndex < _timelineController.Timeline.VideoTracks.Count)
            {
                long totalFrames = _timelineController.GetTotalFramesWithBuffer();
                VideoTrack track = _timelineController.Timeline.VideoTracks[videoTrackIndex];

                Debug.WriteLine($"Attempting to cut at frame: {_timelineController.PositionToFrame(position.X)}");

                foreach (VideoClip clip in track.Clips.ToList())
                {
                    double clipX = _timelineController.FrameToPosition(clip.TimelineStart);
                    double clipWidth = clip.Length * (_timelineController.Timeline.Fps / clip.Source.FPS) / totalFrames * _timelineCanvas.Width;

                    if (position.X >= clipX && position.X <= clipX + clipWidth)
                    {
                        long timelineCutFrame = _timelineController.PositionToFrame(position.X);
                        long cutFrame = (long)((timelineCutFrame - clip.TimelineStart) * (clip.Source.FPS / _timelineController.Timeline.Fps));
                        var (firstPart, secondPart) = clip.SplitAtFrame(cutFrame, _timelineController.Timeline);

                        if (firstPart == null || secondPart == null)
                        {
                            Debug.WriteLine($"Cut failed: Invalid split at frame {cutFrame} for clip {clip.Name}");
                            return;
                        }

                        track.RemoveClip(clip);
                        track.AddClip(firstPart);
                        track.AddClip(secondPart);
                        _clipDrawManager.UpdateClipPositions();
                        _clipDrawManager.HighlightClip(_selectedClip);
                        Debug.WriteLine($"Cut clip: {clip.Name} at frame {cutFrame}");
                        return;
                    }
                }
            }
            else if (audioTrackIndex >= 0 && audioTrackIndex < _timelineController.Timeline.AudioTracks.Count)
            {
                long totalFrames = _timelineController.GetTotalFramesWithBuffer();
                AudioTrack track = _timelineController.Timeline.AudioTracks[audioTrackIndex];

                Debug.WriteLine($"Attempting to cut at frame: {_timelineController.PositionToFrame(position.X)}");

                foreach (AudioClip clip in track.Clips.ToList())
                {
                    double clipX = _timelineController.FrameToPosition(clip.TimelineStart);
                    long frameDuration = (long)(clip.Duration * _timelineController.Timeline.Fps);
                    double clipWidth = (double)frameDuration / totalFrames * _timelineCanvas.Width;

                    if (position.X >= clipX && position.X <= clipX + clipWidth)
                    {
                        long timelineCutFrame = _timelineController.PositionToFrame(position.X);
                        double timelineCutTime = timelineCutFrame / _timelineController.Timeline.Fps;
                        double clipCutTime = timelineCutTime - clip.TimelineStart / _timelineController.Timeline.Fps;
                        Debug.WriteLine($"Cutting Audio Clip at time: {clipCutTime}");
                        var (firstPart, secondPart) = clip.SplitAtTime(clipCutTime, _timelineController.Timeline);

                        if (firstPart == null || secondPart == null)
                        {
                            Debug.WriteLine($"Cut failed: Invalid split at time {clipCutTime} for clip {clip.Name}");
                            return;
                        }

                        track.RemoveClip(clip);
                        track.AddClip(firstPart);
                        track.AddClip(secondPart);
                        _clipDrawManager.UpdateClipPositions();
                        _clipDrawManager.HighlightClip(_selectedClip);
                        Debug.WriteLine($"Cut Audio Clip: {clip.Name} at time {timelineCutTime}");
                        return;
                    }
                }
            }
        }
        public void UpdateSelectedClipX(int x)
        {
            if (_selectedClip is VideoClip videoClip)
            {
                videoClip.X = x;
            }
        }

        public void UpdateSelectedClipY(int y)
        {
            if (_selectedClip is VideoClip videoClip)
            {
                videoClip.Y = y;
            }
        }

        public void UpdateSelectedClipScale(double scale)
        {
            if (_selectedClip is VideoClip videoClip)
            {
                videoClip.Scale = scale;
            }
        }

        public void UpdateSelectedClipOpacity(double opacity)
        {
            if (_selectedClip is VideoClip videoClip)
            {
                videoClip.Opacity = opacity;
            }
        }


    }
    }
