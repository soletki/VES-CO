using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using VESCO.Managers;
using VESCO.Timeline;
using Xabe.FFmpeg;

namespace VESCO
{
    public partial class MainWindow : Window
    {
        private readonly TimelineController _timelineController;
        private readonly PlayheadController _playheadController;
        private readonly ClipSelectionManager _clipSelectionManager;
        private readonly ClipDrawManager _clipDrawManager;
        private readonly TrackManager _trackManager;
        private readonly ToolManager _toolManager;

        public ObservableCollection<SourceFile> MediaBin { get; } = new();

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;

            _timelineController = new TimelineController(60, TimelineArea);
            _playheadController = new PlayheadController(
                _timelineController,
                PlayheadCanvas,
                Playhead,
                PlayheadTop,
                previewImage,
                TimecodeDisplay,
                FrameCounter,
                TimelineScrollViewer);
            _clipDrawManager = new ClipDrawManager(_timelineController, TimelineArea); 
            _trackManager = new TrackManager(_timelineController, TimelineArea, TrackLabelsPanel, _clipDrawManager);
            _clipSelectionManager = new ClipSelectionManager(_timelineController, _clipDrawManager, _trackManager, TimelineArea, XTextBox, XSlider, YTextBox, YSlider, ScaleTextBox, ScaleSlider, OpacityTextBox, OpacitySlider);
            _toolManager = new ToolManager(SelectTool, CutTool);
            
            _trackManager.InitializeTracks();
            _playheadController.UpdateDisplays();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if(Keyboard.FocusedElement is TextBox)
                return;
            switch (e.Key)
            {
                case Key.Space:
                    _playheadController.TogglePlayback();
                    UpdatePlayPauseButton();
                    e.Handled = true;
                    break;
                case Key.OemPeriod:
                    _playheadController.StepForward();
                    e.Handled = true;
                    break;
                case Key.OemComma:
                    _playheadController.StepBackward();
                    e.Handled = true;
                    break;
                case Key.OemPlus:
                case Key.Add:
                    if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
                    {
                        _trackManager.IncreaseTrackHeight();
                        e.Handled = true;
                    }
                    else if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
                    {
                        _timelineController.ZoomIn();
                        _playheadController.UpdatePlayheadPosition();
                        _clipDrawManager.UpdateClipPositions();
                        e.Handled = true;
                    }

                    break;
                case Key.OemMinus:
                case Key.Subtract:
                    if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
                    {
                        _trackManager.DecreaseTrackHeight();
                        e.Handled = true;
                    }
                    else if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
                    {
                        _timelineController.ZoomOut();
                        _playheadController.UpdatePlayheadPosition();
                        _clipDrawManager.UpdateClipPositions();
                        e.Handled = true;
                    }
                    break;
                case Key.Delete:
                    _clipSelectionManager.DeleteSelectedClip();
                    _playheadController.UpdatePreview();
                    e.Handled = true;
                    break;
            }
        }

        private void FrameBackClick(object sender, RoutedEventArgs e)
        {
            _playheadController.StepBackward();
        }

        private void FrameForwardClick(object sender, RoutedEventArgs e)
        {
            _playheadController.StepForward();
        }

        private void PlayPauseClick(object sender, RoutedEventArgs e)
        {
            _playheadController.TogglePlayback();
            UpdatePlayPauseButton();
        }

        private void UpdatePlayPauseButton()
        {
            PlayPause.Content = _playheadController.IsPlaying ? "⏸" : "▶";
        }

        private async void OpenVideoClick(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Video Files|*.mp4;*.avi;*.mov;*.mkv"
            };

            if (dialog.ShowDialog() == true)
            {
                MediaBin.Add(new VideoSource(dialog.FileName));
                IMediaInfo mediaInfo = await Xabe.FFmpeg.FFmpeg.GetMediaInfo(dialog.FileName);
                var audioStream = mediaInfo.AudioStreams.ToList();
                if (audioStream.Count > 0)
                {
                    string tempDir = Path.Combine(Path.GetTempPath(), "vesco_audio_" + Guid.NewGuid());
                    Directory.CreateDirectory(tempDir);
                    for (int i = 0; i < audioStream.Count; i++)
                    {
                        var conversion = Xabe.FFmpeg.FFmpeg.Conversions.New()
                            .AddStream(audioStream)
                            .SetOutput(tempDir + $"_{i}.wav")
                            .SetOverwriteOutput(true);

                        await conversion.Start();
                    }
                    for (int i = 0; i < audioStream.Count; i++)
                    {
                        MediaBin.Add(new AudioSource(tempDir + $"_{i}.wav"));
                    }
                }
            }
        }

        private void MediaBinMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (MediaBinList.SelectedItem is SourceFile source)
            {
                DragDrop.DoDragDrop(MediaBinList, source, DragDropEffects.Copy);
            }
        }

        private void TimelineDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(typeof(VideoSource)))
            {
                var source = (VideoSource)e.Data.GetData(typeof(VideoSource));
                var dropPosition = e.GetPosition(TimelineArea);

                _trackManager.AddVideoClipAtPosition(source, dropPosition.X, dropPosition.Y);
                _playheadController.UpdatePreview();
            }
            else if (e.Data.GetDataPresent(typeof(AudioSource)))
            {
                var source = (AudioSource)e.Data.GetData(typeof(AudioSource));
                var dropPosition = e.GetPosition(TimelineArea);

                _trackManager.AddAudioClipAtPosition(source, dropPosition.X, dropPosition.Y);
                _playheadController.UpdatePreview();
            }
        }

        private void TimelineClick(object sender, MouseButtonEventArgs e)
        {
            var position = e.GetPosition(TimelineArea);
            Debug.WriteLine($"Timeline clicked at: {position}");

            if (_toolManager.ActiveTool == ToolType.Select)
            {
                _clipSelectionManager.HandleTimelineClickSelect(position);
            }
            else if (_toolManager.ActiveTool == ToolType.Cut)
            {
                _clipSelectionManager.HandleTimelineClickCut(position);
            }
            else
            {
                _playheadController.StartDragging(position);
                _clipSelectionManager.SelectClipAtPosition(position);
                Mouse.Capture(TimelineArea);
            }
        }

        private void TimelineMove(object sender, MouseEventArgs e)
        {
            var position = e.GetPosition(TimelineArea);

            if (_clipSelectionManager.IsDragging)
            {
                _clipSelectionManager.HandleDrag(position);
                _playheadController.UpdatePreview();
            }
            else if (_playheadController.IsDragging)
            {
                _playheadController.UpdateFromMouse(position);
            }
        }

        private void TimelineRelease(object sender, MouseButtonEventArgs e)
        {
            _clipSelectionManager.EndDrag();
            _playheadController.EndDragging();
            Mouse.Capture(null);
        }

        private void SelectToolClick(object sender, RoutedEventArgs e)
        {
            _toolManager.ToggleTool(ToolType.Select);
        }

        private void CutToolClick(object sender, RoutedEventArgs e)
        {
            _toolManager.ToggleTool(ToolType.Cut);
        }

        private void AddTrackClick(object sender, RoutedEventArgs e)
        {
            _trackManager.AddVideoTrack();
            _trackManager.AddAudioTrack();
        }

        private void TimelineScrollChanged(object sender, System.Windows.Controls.ScrollChangedEventArgs e)
        {
            if (e.VerticalChange != 0)
            {
                LabelsScrollViewer.ScrollToVerticalOffset(e.VerticalOffset);
            }
            if (e.HorizontalOffset != 0)
            {
                _playheadController.UpdatePlayheadPosition();
            }
        }

        private void OnPreviewScaleChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_timelineController == null)
                return;
            switch (PreviewScaleDropdown.SelectedIndex)
            {
                case 0:
                    _timelineController.Timeline.PreviewScale = 1;
                    break;
                case 1:
                    _timelineController.Timeline.PreviewScale = 0.5;
                    break;
                case 2:
                    _timelineController.Timeline.PreviewScale = 0.25;
                    break;
                case 3:
                    _timelineController.Timeline.PreviewScale = 0.125;
                    break;
                default:
                    break;
            }
        }

        private void SeekStartClick(object sender, RoutedEventArgs e)
        {
            _playheadController.Pause();
            _playheadController.UpdateCurrentFrame(0);
            _playheadController.UpdatePlayheadPosition();
            _playheadController.UpdatePreview();
            _playheadController.UpdateDisplays();
        }

        private void SeekEndClick(object sender, RoutedEventArgs e)
        {
            _playheadController.Pause();
            _playheadController.UpdateCurrentFrame(_timelineController.Timeline.GetTotalFrames() - 1);
            _playheadController.UpdatePlayheadPosition();
            _playheadController.UpdatePreview();
            _playheadController.UpdateDisplays();
        }

        private void ExportVideoClick(object sender, RoutedEventArgs e)
        {
            var exportWindow = new ExportWindow(_timelineController.Timeline);
            exportWindow.Owner = this;
            exportWindow.ShowDialog();
        }

        private void XTextBoxKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (int.TryParse(XTextBox.Text, out int x))
                {
                    _clipSelectionManager.UpdateSelectedClipX(x);
                    _playheadController.UpdatePreview();
                }
            }
        }

        private void YTextBoxKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (int.TryParse(YTextBox.Text, out int y))
                {
                    _clipSelectionManager.UpdateSelectedClipY(y);
                    _playheadController.UpdatePreview();
                }
            }
        }

        private void ScaleTextBoxKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (double.TryParse(ScaleTextBox.Text, out double scale))
                {
                    _clipSelectionManager.UpdateSelectedClipScale(scale);
                    ScaleSlider.Value = scale;
                    _playheadController.UpdatePreview();
                }
            }
        }

        private void OpacityTextBoxKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (double.TryParse(OpacityTextBox.Text, out double opacity))
                {
                    _clipSelectionManager.UpdateSelectedClipOpacity(opacity);
                    OpacitySlider.Value = opacity;
                    _playheadController.UpdatePreview();
                }
            }
        }

        private void OpacitySliderValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (OpacityTextBox != null)
            {
                double opacity = e.NewValue;
                OpacityTextBox.Text = opacity.ToString("F2");
                _clipSelectionManager.UpdateSelectedClipOpacity(opacity);
                _playheadController.UpdatePreview();
            }
        }

        private void ScaleSliderValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (ScaleTextBox != null)
            {
                double scale = e.NewValue;
                ScaleTextBox.Text = scale.ToString("F2");
                _clipSelectionManager.UpdateSelectedClipScale(scale);
                _playheadController.UpdatePreview();
            }
        }

        private void XSliderValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (OpacityTextBox != null)
            {
                double x = e.NewValue;
                XTextBox.Text = x.ToString("F0");
                _clipSelectionManager.UpdateSelectedClipX((int)x);
                _playheadController.UpdatePreview();
            }
        }

        private void YSliderValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (OpacityTextBox != null)
            {
                double y = e.NewValue;
                YTextBox.Text = y.ToString("F0");
                _clipSelectionManager.UpdateSelectedClipY((int)y);
                _playheadController.UpdatePreview();
            }
        }

        private void Exit(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void ClearTimeline(object sender, RoutedEventArgs e)
        {
            _trackManager.ClearTracks();
        }
    }
}