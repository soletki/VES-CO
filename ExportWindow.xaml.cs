using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;

namespace VESCO
{
    public partial class ExportWindow : Window
    {
        private Timeline.Timeline _timeline;
        private CancellationTokenSource ?_cancellationTokenSource;

        public ExportWindow(Timeline.Timeline timeline)
        {
            InitializeComponent();
            _timeline = timeline;

            OutputPathTextBox.Text = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
                $"Export_{DateTime.Now:yyyyMMdd_HHmmss}.mp4");

            ResolutionComboBox.SelectionChanged += ResolutionComboBoxSelectionChanged;
        }

        private void BrowseOutputPathClick(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog
            {
                Filter = "MP4 Video|*.mp4|MKV Video|*.mkv|WebM Video|*.webm|All Files|*.*",
                DefaultExt = ".mp4",
                FileName = System.IO.Path.GetFileName(OutputPathTextBox.Text)
            };

            if (dialog.ShowDialog() == true)
            {
                OutputPathTextBox.Text = dialog.FileName;
            }
        }

        private void ResolutionComboBoxSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ResolutionComboBox.SelectedItem is ComboBoxItem item)
            {
                CustomResolutionPanel.Visibility = item.Tag?.ToString() == "custom"
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
        }

        private void QualitySliderValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (QualityValueText == null) return;

            int value = (int)e.NewValue;
            string quality = value switch
            {
                <= 10 => "Excellent",
                <= 18 => "High",
                <= 23 => "Medium",
                <= 28 => "Low",
                _ => "Very Low"
            };

            QualityValueText.Text = $"{value} ({quality})";
        }

        private async void ExportClick(object sender, RoutedEventArgs e)
        {
            // Validate output path
            if (string.IsNullOrWhiteSpace(OutputPathTextBox.Text))
            {
                MessageBox.Show("Please select an output file.", "Export Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Get settings
            var settings = GetExportSettings();

            // Create cancellation token
            _cancellationTokenSource = new CancellationTokenSource();

            // Disable export button, enable cancel
            ExportButton.IsEnabled = false;
            ProgressPanel.Visibility = Visibility.Visible;

            try
            {
                var renderer = new VideoRenderer();
                renderer.OnProgress += (progress) =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        ExportProgressBar.Value = progress;
                        ProgressText.Text = $"Exporting: {progress:F1}%";
                    });
                };

                await renderer.RenderVideo(
                    _timeline,
                    settings.OutputPath,
                    settings.Width,
                    settings.Height,
                    settings.Fps,
                    settings.Codec,
                    settings.Quality,
                    settings.PixelFormat,
                    _cancellationTokenSource.Token);

                if (!_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    MessageBox.Show("Export completed successfully!", "Export Complete",
                        MessageBoxButton.OK, MessageBoxImage.Information);

                    DialogResult = true;
                    Close();
                }
            }
            catch (OperationCanceledException)
            {
                MessageBox.Show("Export was cancelled.", "Export Cancelled",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Export failed: {ex.Message}", "Export Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                ExportButton.IsEnabled = true;
                ProgressPanel.Visibility = Visibility.Collapsed;
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
            }
        }

        private void CancelClick(object sender, RoutedEventArgs e)
        {
            // If export is in progress, cancel it
            if (_cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested)
            {
                var result = MessageBox.Show(
                    "Are you sure you want to cancel the export?",
                    "Cancel Export",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    _cancellationTokenSource.Cancel();
                    ProgressText.Text = "Cancelling export...";
                }
            }
            else
            {
                // No export in progress, just close
                DialogResult = false;
                Close();
            }
        }

        private ExportSettings GetExportSettings()
        {
            var settings = new ExportSettings
            {
                OutputPath = OutputPathTextBox.Text
            };

            // Resolution
            if (ResolutionComboBox.SelectedItem is ComboBoxItem resItem)
            {
                if (resItem.Tag?.ToString() == "custom")
                {
                    settings.Width = int.Parse(CustomWidthTextBox.Text);
                    settings.Height = int.Parse(CustomHeightTextBox.Text);
                }
                else
                {
                    var parts = resItem.Tag?.ToString()?.Split('x');
                    settings.Width = int.Parse(parts[0]);
                    settings.Height = int.Parse(parts[1]);
                }
            }

            // FPS
            if (FpsComboBox.SelectedItem is ComboBoxItem fpsItem)
            {
                settings.Fps = fpsItem.Tag?.ToString() == "timeline"
                    ? _timeline.Fps
                    : double.Parse(fpsItem.Tag?.ToString() ?? "30");
            }

            // Codec
            if (CodecComboBox.SelectedItem is ComboBoxItem codecItem)
            {
                settings.Codec = codecItem.Tag?.ToString() ?? "libx264";
            }

            // Quality
            settings.Quality = (int)QualitySlider.Value;

            // Pixel Format
            if (PixelFormatComboBox.SelectedItem is ComboBoxItem pixelItem)
            {
                settings.PixelFormat = pixelItem.Tag?.ToString() ?? "yuv420p";
            }

            return settings;
        }
    }

    public class ExportSettings
    {
        public string OutputPath { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public double Fps { get; set; }
        public string Codec { get; set; }
        public int Quality { get; set; }
        public string PixelFormat { get; set; }
    }
}