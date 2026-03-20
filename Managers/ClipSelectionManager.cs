using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using VESCO.Timeline;

namespace VESCO.Managers
{
    public class ClipSelectionManager
    {
        private Clip? _selectedClip;
        private int _selectedTrackIndex = -1;
        private readonly TimelineController _timelineController;
        private readonly ClipDrawManager _clipDrawManager;
        private readonly TrackManager _trackManager;
        private readonly SnapManager _snapManager;
        private bool _isDragging = false;
        private Point _dragStartMouse;
        private long _dragStartFrame;
        private readonly Canvas _timelineCanvas;
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
            _snapManager = new SnapManager(timelineController);
        }

        public void DeleteSelectedClip()
        {
            if (_selectedClip != null && _selectedTrackIndex >= 0)
            {
                if (_selectedClip is AudioClip audioClip)
                {
                    _timelineController.Timeline.AudioTracks[_selectedTrackIndex].RemoveClip(audioClip);
                }
                else if (_selectedClip is VideoClip videoClip)
                {
                    _timelineController.Timeline.VideoTracks[_selectedTrackIndex].RemoveClip(videoClip);
                    videoClip.Dispose();
                }
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

            long targetFrame = Math.Max(0, _dragStartFrame + deltaFrames);
            
            // Check if Shift key is pressed to disable snapping
            bool enableSnapping = (Keyboard.Modifiers & ModifierKeys.Shift) == 0;
            long snappedFrame = _snapManager.GetSnappedFrame(_selectedClip, _selectedTrackIndex, targetFrame, enableSnapping);

            _selectedClip.TimelineStart = snappedFrame;
            TryMoveSelectedClipToTrack(position.Y);

            _clipDrawManager.UpdateClipPositions();
            _clipDrawManager.HighlightClip(_selectedClip);
        }

        public void EndDrag()
        {
            if (_isDragging)
            {
                _isDragging = false;
                _timelineCanvas.ReleaseMouseCapture();
                _snapManager.ResetSnapState();
                Debug.WriteLine($"Clip dropped: {_selectedClip?.Name}");
            }
        }

        public void SelectClipAtPosition(Point position)
        {
            int videoTrackIndex = _trackManager.GetVideoTrackIndexFromY(position.Y);
            int audioTrackIndex = _trackManager.GetAudioTrackIndexFromY(position.Y);
            if (!(audioTrackIndex >= 0 && audioTrackIndex < _timelineController.Timeline.AudioTracks.Count) && !(videoTrackIndex >= 0 && videoTrackIndex <= _timelineController.Timeline.VideoTracks.Count))
            {
                ClearSelection();
                return;
            }

            if (videoTrackIndex >= 0 && videoTrackIndex < _timelineController.Timeline.VideoTracks.Count)
            {
                VideoTrack track = _timelineController.Timeline.VideoTracks[videoTrackIndex];

                foreach (VideoClip clip in track.Clips)
                {
                    if (IsPointOnVideoClip(position.X, clip))
                    {
                        _selectedClip = clip;
                        SetVideoControlsForClip(clip);
                        _selectedTrackIndex = videoTrackIndex;
                        Debug.WriteLine($"Selected clip: {clip.Name} on track {videoTrackIndex}");
                        _clipDrawManager.HighlightClip(_selectedClip);
                        return;
                    }
                }

                ClearSelection();
            }
            else if (audioTrackIndex >= 0 && audioTrackIndex < _timelineController.Timeline.AudioTracks.Count)
            {
                AudioTrack track = _timelineController.Timeline.AudioTracks[audioTrackIndex];

                foreach (AudioClip clip in track.Clips)
                {
                    if (IsPointOnAudioClip(position.X, clip))
                    {
                        SetVideoControlsEnabled(false);
                        _selectedClip = clip;
                        _selectedTrackIndex = audioTrackIndex;
                        Debug.WriteLine($"Selected clip: {clip.Name} on track {audioTrackIndex}");
                        _clipDrawManager.HighlightClip(_selectedClip);
                        return;
                    }
                }

                ClearSelection();
            }


        }

        private void CutClipAtPosition(Point position)
        {
            int videoTrackIndex = _trackManager.GetVideoTrackIndexFromY(position.Y);
            int audioTrackIndex = _trackManager.GetAudioTrackIndexFromY(position.Y);

            if (videoTrackIndex >= 0 && videoTrackIndex < _timelineController.Timeline.VideoTracks.Count)
            {
                VideoTrack track = _timelineController.Timeline.VideoTracks[videoTrackIndex];

                Debug.WriteLine($"Attempting to cut at frame: {_timelineController.PositionToFrame(position.X)}");

                foreach (VideoClip clip in track.Clips.ToList())
                {
                    if (IsPointOnVideoClip(position.X, clip))
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
                        clip.Dispose();
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
                AudioTrack track = _timelineController.Timeline.AudioTracks[audioTrackIndex];

                Debug.WriteLine($"Attempting to cut at frame: {_timelineController.PositionToFrame(position.X)}");

                foreach (AudioClip clip in track.Clips.ToList())
                {
                    if (IsPointOnAudioClip(position.X, clip))
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

        private void TryMoveSelectedClipToTrack(double yPosition)
        {
            if (_selectedClip is VideoClip videoClip)
            {
                int newTrackIndex = _trackManager.GetVideoTrackIndexFromY(yPosition);
                if (newTrackIndex >= 0 &&
                    newTrackIndex < _timelineController.Timeline.VideoTracks.Count &&
                    newTrackIndex != _selectedTrackIndex)
                {
                    _timelineController.Timeline.VideoTracks[_selectedTrackIndex].RemoveClip(videoClip);
                    _timelineController.Timeline.VideoTracks[newTrackIndex].AddClip(videoClip);
                    _selectedTrackIndex = newTrackIndex;
                }

                return;
            }

            if (_selectedClip is AudioClip audioClip)
            {
                int newTrackIndex = _trackManager.GetAudioTrackIndexFromY(yPosition);
                if (newTrackIndex >= 0 &&
                    newTrackIndex < _timelineController.Timeline.AudioTracks.Count &&
                    newTrackIndex != _selectedTrackIndex)
                {
                    _timelineController.Timeline.AudioTracks[_selectedTrackIndex].RemoveClip(audioClip);
                    _timelineController.Timeline.AudioTracks[newTrackIndex].AddClip(audioClip);
                    _selectedTrackIndex = newTrackIndex;
                }
            }
        }

        private bool IsPointOnVideoClip(double positionX, VideoClip clip)
        {
            double clipX = _timelineController.FrameToPosition(clip.TimelineStart);
            double clipWidth = GetVideoClipWidth(clip);
            return positionX >= clipX && positionX <= clipX + clipWidth;
        }

        private bool IsPointOnAudioClip(double positionX, AudioClip clip)
        {
            double clipX = _timelineController.FrameToPosition(clip.TimelineStart);
            double clipWidth = GetAudioClipWidth(clip);
            return positionX >= clipX && positionX <= clipX + clipWidth;
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

        private void SetVideoControlsForClip(VideoClip clip)
        {
            SetVideoControlsEnabled(true);

            _xTextBox.Text = clip.X.ToString();
            _xSlider.Minimum = -clip.Source.Width;
            _xSlider.Maximum = clip.Source.Width;
            _xSlider.Value = clip.X;

            _yTextBox.Text = clip.Y.ToString();
            _ySlider.Minimum = -clip.Source.Height;
            _ySlider.Maximum = clip.Source.Height;
            _ySlider.Value = clip.Y;

            _scaleTextBox.Text = clip.Scale.ToString("F2");
            _scaleSlider.Value = clip.Scale;

            _opacityTextBox.Text = clip.Opacity.ToString("F2");
            _opacitySlider.Value = clip.Opacity;
        }

        private void SetVideoControlsEnabled(bool enabled)
        {
            _xTextBox.IsEnabled = enabled;
            _xSlider.IsEnabled = enabled;
            _yTextBox.IsEnabled = enabled;
            _ySlider.IsEnabled = enabled;
            _scaleTextBox.IsEnabled = enabled;
            _scaleSlider.IsEnabled = enabled;
            _opacityTextBox.IsEnabled = enabled;
            _opacitySlider.IsEnabled = enabled;
        }

        private void ClearSelection()
        {
            _selectedClip = null;
            _selectedTrackIndex = -1;
            SetVideoControlsEnabled(false);
            _clipDrawManager.ClearHighlights();
        }

    }
}
