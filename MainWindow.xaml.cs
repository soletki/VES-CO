using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
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

            _timelineController = new TimelineController(15, TimelineArea);
            _playheadController = new PlayheadController(_timelineController, Playhead, previewImage);
            _clipManager = new ClipManager(_timelineController, TimelineArea);
            _toolManager = new ToolManager(SelectTool, CutTool);

            InitializeEventHandlers();
        }

        private void InitializeEventHandlers()
        {
            _clipManager.ClipSelected += (clip) => _playheadController.UpdatePreview();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            switch (e.Key)
            {
                case Key.OemPeriod:
                    _playheadController.StepForward();
                    e.Handled = true;
                    break;
                case Key.OemComma:
                    _playheadController.StepBackward();
                    e.Handled = true;
                    break;
                case Key.OemPlus:
                    _timelineController.ZoomIn();
                    _playheadController.UpdatePlayheadPosition();
                    _clipManager.UpdateClipPositions();
                    break;
                case Key.OemMinus:
                    _timelineController.ZoomOut();
                    _playheadController.UpdatePlayheadPosition();
                    _clipManager.UpdateClipPositions();
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

                _clipManager.AddClipAtPosition(source, dropPosition.X);
                _playheadController.UpdatePreview();
            }
        }

        private void TimelineClick(object sender, MouseButtonEventArgs e)
        {
            var position = e.GetPosition(TimelineArea);

            if (_toolManager.ActiveTool == ToolType.Select)
            {
                _clipManager.HandleTimelineClickSelect(position);
            }
            else if(_toolManager.ActiveTool == ToolType.Cut)
            {
                _clipManager.HandleTimelineClickCut(position);
            }
            else
            {
                _playheadController.StartDragging(position);
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
        }

        private void SelectToolClick(object sender, RoutedEventArgs e)
        {
            _toolManager.ToggleTool(ToolType.Select);
        }

        private void CutToolClick(object sender, RoutedEventArgs e)
        {
            _toolManager.ToggleTool(ToolType.Cut);
        }
    }
}