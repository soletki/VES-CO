using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using VESCO.Timeline;

namespace VESCO
{
    public partial class MainWindow : Window
    {
        private readonly TimelineController _timelineController;
        private readonly PlayheadController _playheadController;
        private readonly ClipManager _clipManager;
        private readonly ToolManager _toolManager;

        public ObservableCollection<SourceMedia> MediaBin { get; } = new();

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
            _clipManager = new ClipManager(_timelineController, TimelineArea, TrackLabelsPanel, XTextBox, YTextBox, ScaleTextBox, OpacityTextBox);
            _toolManager = new ToolManager(SelectTool, CutTool);

            InitializeEventHandlers();
            _clipManager.InitializeTracks();
            _playheadController.UpdateDisplays();
        }

        private void InitializeEventHandlers()
        {
            _clipManager.ClipSelected += (clip) => _playheadController.UpdatePreview();
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
                        _clipManager.IncreaseTrackHeight();
                        e.Handled = true;
                    }
                    else if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
                    {
                        _timelineController.ZoomIn();
                        _playheadController.UpdatePlayheadPosition();
                        _clipManager.UpdateClipPositions();
                        e.Handled = true;
                    }

                    break;
                case Key.OemMinus:
                case Key.Subtract:
                    if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
                    {
                        _clipManager.DecreaseTrackHeight();
                        e.Handled = true;
                    }
                    else if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
                    {
                        _timelineController.ZoomOut();
                        _playheadController.UpdatePlayheadPosition();
                        _clipManager.UpdateClipPositions();
                        e.Handled = true;
                    }
                    break;
                case Key.Delete:
                    _clipManager.DeleteSelectedClip();
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

        private void OpenVideo_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Video Files|*.mp4;*.avi;*.mov;*.mkv"
            };

            if (dialog.ShowDialog() == true)
            {
                MediaBin.Add(new SourceMedia(dialog.FileName));
            }
        }

        private void MediaBinMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (MediaBinList.SelectedItem is SourceMedia source)
            {
                DragDrop.DoDragDrop(MediaBinList, source, DragDropEffects.Copy);
            }
        }

        private void TimelineDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(typeof(SourceMedia)))
            {
                var source = (SourceMedia)e.Data.GetData(typeof(SourceMedia));
                var dropPosition = e.GetPosition(TimelineArea);

                _clipManager.AddClipAtPosition(source, dropPosition.X, dropPosition.Y);
                _playheadController.UpdatePreview();
            }
        }

        private void TimelineClick(object sender, MouseButtonEventArgs e)
        {
            var position = e.GetPosition(TimelineArea);
            Debug.WriteLine($"Timeline clicked at: {position}");

            if (_toolManager.ActiveTool == ToolType.Select)
            {
                _clipManager.HandleTimelineClickSelect(position);
            }
            else if (_toolManager.ActiveTool == ToolType.Cut)
            {
                _clipManager.HandleTimelineClickCut(position);
            }
            else
            {
                _playheadController.StartDragging(position);
                _clipManager.SelectClipAtPosition(position);
                Mouse.Capture(TimelineArea);
            }
        }

        private void TimelineMove(object sender, MouseEventArgs e)
        {
            var position = e.GetPosition(TimelineArea);

            if (_clipManager.IsDragging)
            {
                _clipManager.HandleDrag(position);
                _playheadController.UpdatePreview();
            }
            else if (_playheadController.IsDragging)
            {
                _playheadController.UpdateFromMouse(position);
            }
        }

        private void TimelineRelease(object sender, MouseButtonEventArgs e)
        {
            _clipManager.EndDrag();
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
            _clipManager.AddVideoTrack();
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
                    _clipManager.UpdateSelectedClipX(x);
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
                    _clipManager.UpdateSelectedClipY(y);
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
                    _clipManager.UpdateSelectedClipScale(scale);
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
                    _clipManager.UpdateSelectedClipOpacity(opacity);
                    _playheadController.UpdatePreview();
                }
            }
        }
    }
}