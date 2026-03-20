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
                        ApplyZoom(_timelineController.ZoomIn);
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
                        ApplyZoom(_timelineController.ZoomOut);
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
                            .AddStream(audioStream[i])
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
            var dropPosition = e.GetPosition(TimelineArea);

            if (e.Data.GetDataPresent(typeof(VideoSource)))
            {
                var source = (VideoSource)e.Data.GetData(typeof(VideoSource));
                _trackManager.AddVideoClipAtPosition(source, dropPosition.X, dropPosition.Y);
                _playheadController.UpdatePreview();
            }
            else if (e.Data.GetDataPresent(typeof(AudioSource)))
            {
                var source = (AudioSource)e.Data.GetData(typeof(AudioSource));
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
            SeekToFrame(0);
        }

        private void SeekEndClick(object sender, RoutedEventArgs e)
        {
            SeekToFrame(_timelineController.Timeline.GetTotalFrames() - 1);
        }

        private void ExportVideoClick(object sender, RoutedEventArgs e)
        {
            var exportWindow = new ExportWindow(_timelineController.Timeline);
            exportWindow.Owner = this;
            exportWindow.ShowDialog();
        }

        private void XTextBoxKeyDown(object sender, KeyEventArgs e)
        {
            HandleIntegerTextBoxEnter(e, XTextBox, value => _clipSelectionManager.UpdateSelectedClipX(value));
        }

        private void YTextBoxKeyDown(object sender, KeyEventArgs e)
        {
            HandleIntegerTextBoxEnter(e, YTextBox, value => _clipSelectionManager.UpdateSelectedClipY(value));
        }

        private void ScaleTextBoxKeyDown(object sender, KeyEventArgs e)
        {
            HandleDoubleTextBoxEnter(e, ScaleTextBox, value => _clipSelectionManager.UpdateSelectedClipScale(value), ScaleSlider, "F2");
        }

        private void OpacityTextBoxKeyDown(object sender, KeyEventArgs e)
        {
            HandleDoubleTextBoxEnter(e, OpacityTextBox, value => _clipSelectionManager.UpdateSelectedClipOpacity(value), OpacitySlider, "F2");
        }

        private void OpacitySliderValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            HandleSliderValueChanged(OpacityTextBox, e.NewValue, "F2", value => _clipSelectionManager.UpdateSelectedClipOpacity(value));
        }

        private void ScaleSliderValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            HandleSliderValueChanged(ScaleTextBox, e.NewValue, "F2", value => _clipSelectionManager.UpdateSelectedClipScale(value));
        }

        private void XSliderValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            HandleSliderValueChanged(XTextBox, e.NewValue, "F0", value => _clipSelectionManager.UpdateSelectedClipX((int)value));
        }

        private void YSliderValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            HandleSliderValueChanged(YTextBox, e.NewValue, "F0", value => _clipSelectionManager.UpdateSelectedClipY((int)value));
        }

        private void Exit(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void ClearTimeline(object sender, RoutedEventArgs e)
        {
            _trackManager.ClearTracks();
        }

        private void ApplyZoom(Action zoomAction)
        {
            zoomAction();
            _playheadController.UpdatePlayheadPosition();
            _clipDrawManager.UpdateClipPositions();
        }

        private void SeekToFrame(long frame)
        {
            _playheadController.Pause();
            _playheadController.UpdateCurrentFrame(frame);
            _playheadController.UpdatePlayheadPosition();
            _playheadController.UpdatePreview();
            _playheadController.UpdateDisplays();
        }

        private void HandleIntegerTextBoxEnter(KeyEventArgs e, TextBox textBox, Action<int> applyValue)
        {
            if (!ManagersInitialized())
            {
                return;
            }

            if (e.Key != Key.Enter)
            {
                return;
            }

            if (int.TryParse(textBox.Text, out int value))
            {
                applyValue(value);
                _playheadController.UpdatePreview();
            }
        }

        private void HandleDoubleTextBoxEnter(KeyEventArgs e, TextBox textBox, Action<double> applyValue, Slider? slider, string format)
        {
            if (!ManagersInitialized())
            {
                return;
            }

            if (e.Key != Key.Enter)
            {
                return;
            }

            if (double.TryParse(textBox.Text, out double value))
            {
                applyValue(value);
                if (slider != null)
                {
                    slider.Value = value;
                }

                textBox.Text = value.ToString(format);
                _playheadController.UpdatePreview();
            }
        }

        private void HandleSliderValueChanged(TextBox? textBox, double value, string format, Action<double> applyValue)
        {
            if (!ManagersInitialized())
            {
                return;
            }

            if (textBox == null)
            {
                return;
            }

            textBox.Text = value.ToString(format);
            applyValue(value);
            _playheadController.UpdatePreview();
        }

        private bool ManagersInitialized()
        {
            return _clipSelectionManager != null && _playheadController != null;
        }
    }
}
