using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using MMS.Client.Services;
using System.Diagnostics;

namespace MMS.Client
{
    public partial class MainWindow : Window
    {
        private const string ServerBaseUrl = "https://localhost:7238/api/Image";

        private readonly HttpClient _httpClient = new();
        private readonly ClientLogger _logger = new();
        private const string CurrentUser = "Petar";

        private byte[]? _originalImageBytes;
        private byte[]? _currentImageBytes;

        private readonly Stack<byte[]> _undoStack = new();
        private readonly Stack<byte[]> _redoStack = new();
        private double _zoomFactor = 1.0;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void SetStatus(string message)
        {
            StatusTextBlock.Text = message;
        }

        private static BitmapImage LoadBitmap(byte[] bytes)
        {
            BitmapImage bitmap = new();

            using MemoryStream stream = new(bytes);

            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
            bitmap.Freeze();

            return bitmap;
        }

        private void ShowOriginal(byte[] bytes)
        {
            OriginalImage.Source = LoadBitmap(bytes);
        }

        private void ShowResult(byte[] bytes)
        {
            ResultImage.Source = LoadBitmap(bytes);
        }

        private static ByteArrayContent CreateImageContent(byte[] imageBytes, string fileName)
        {
            var content = new ByteArrayContent(imageBytes);
            content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            content.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data")
            {
                Name = "\"image\"",
                FileName = $"\"{fileName}\""
            };

            return content;
        }

        private async Task<byte[]> PostImageAsync(string endpoint, Dictionary<string, string>? fields = null)
        {
            if (_currentImageBytes == null)
                throw new InvalidOperationException("Nema učitane slike.");

            using MultipartFormDataContent form = new();

            form.Add(CreateImageContent(_currentImageBytes, "client-image.png"), "image", "client-image.png");

            if (fields != null)
            {
                foreach (var field in fields)
                    form.Add(new StringContent(field.Value), field.Key);
            }

            HttpResponseMessage response = await _httpClient.PostAsync($"{ServerBaseUrl}/{endpoint}", form);

            if (!response.IsSuccessStatusCode)
            {
                string error = await response.Content.ReadAsStringAsync();
                throw new InvalidOperationException(error);
            }

            return await response.Content.ReadAsByteArrayAsync();
        }

        private async void OpenImageButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new()
            {
                Filter = "Image files|*.png;*.jpg;*.jpeg;*.bmp;*.gif"
            };

            if (dialog.ShowDialog() != true)
                return;

            _originalImageBytes = await File.ReadAllBytesAsync(dialog.FileName);
            _currentImageBytes = _originalImageBytes;

            _undoStack.Clear();
            _redoStack.Clear();

            ShowOriginal(_originalImageBytes);
            ShowResult(_currentImageBytes);

            SetStatus($"Loaded: {Path.GetFileName(dialog.FileName)}");

            await _logger.InfoAsync(
                CurrentUser,
                "OpenImage",
                $"File={Path.GetFileName(dialog.FileName)}, Size={_currentImageBytes.Length}");
        }

        private async void OpenMsiButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new()
            {
                Filter = "MSI files|*.msi"
            };

            if (dialog.ShowDialog() != true)
                return;

            try
            {
                Stopwatch stopwatch = Stopwatch.StartNew();

                SetStatus("Importing MSI...");

                byte[] msiBytes = await File.ReadAllBytesAsync(dialog.FileName);

                using MultipartFormDataContent form = new();
                var fileContent = new ByteArrayContent(msiBytes);
                fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

                form.Add(fileContent, "file", Path.GetFileName(dialog.FileName));

                HttpResponseMessage response = await _httpClient.PostAsync($"{ServerBaseUrl}/import-msi", form);

                if (!response.IsSuccessStatusCode)
                {
                    string error = await response.Content.ReadAsStringAsync();
                    throw new InvalidOperationException(error);
                }

                byte[] pngBytes = await response.Content.ReadAsByteArrayAsync();

                _originalImageBytes = pngBytes;
                _currentImageBytes = pngBytes;

                _undoStack.Clear();
                _redoStack.Clear();

                ShowOriginal(pngBytes);
                ShowResult(pngBytes);

                SetStatus("MSI imported.");

                stopwatch.Stop();

                await _logger.InfoAsync(
                    CurrentUser,
                    "OpenMSI",
                    $"File={Path.GetFileName(dialog.FileName)}",
                    stopwatch.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                await _logger.ErrorAsync(
                    CurrentUser,
                    "OpenMSI",
                    "MSI import failed",
                    ex);

                MessageBox.Show(ex.Message, "MSI import error", MessageBoxButton.OK, MessageBoxImage.Error);
                SetStatus("MSI import failed.");
            }
        }

        private async void ApplyFilterButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentImageBytes == null)
            {
                MessageBox.Show("Prvo učitaj sliku.");
                return;
            }

            try
            {
                Stopwatch stopwatch = Stopwatch.StartNew();
                string filter = ((System.Windows.Controls.ComboBoxItem)FilterComboBox.SelectedItem)
                    .Content
                    .ToString()!;

                Dictionary<string, string> fields = new();

                string endpoint = filter;

                if (filter == "brightness")
                    fields["amount"] = AmountTextBox.Text;

                if (filter == "pixelate")
                    fields["blockSize"] = BlockSizeTextBox.Text;

                if (filter == "time-warp")
                    fields["factor"] = FactorTextBox.Text;

                SetStatus($"Applying {filter}...");

                byte[] before = _currentImageBytes;

                byte[] result = await PostImageAsync(endpoint, fields);

                _undoStack.Push(before);

                while (_undoStack.Count > 3)
                {
                    var temp = _undoStack.ToArray();
                    _undoStack.Clear();

                    for (int i = Math.Min(2, temp.Length - 1); i >= 0; i--)
                        _undoStack.Push(temp[i]);
                }

                _redoStack.Clear();

                _currentImageBytes = result;
                ShowResult(result);

                SetStatus($"Applied: {filter}");

                stopwatch.Stop();

                await _logger.InfoAsync(
                    CurrentUser,
                    "ApplyFilter",
                    $"Filter={filter}",
                    stopwatch.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                await _logger.ErrorAsync(
                    CurrentUser,
                    "ApplyFilter",
                    "Filter failed",
                    ex);

                MessageBox.Show(ex.Message, "Filter error", MessageBoxButton.OK, MessageBoxImage.Error);
                SetStatus("Filter failed.");
            }
        }

        private void UndoButton_Click(object sender, RoutedEventArgs e)
        {
            if (_undoStack.Count == 0 || _currentImageBytes == null)
            {
                SetStatus("Nothing to undo.");
                return;
            }

            _redoStack.Push(_currentImageBytes);

            _currentImageBytes = _undoStack.Pop();
            ShowResult(_currentImageBytes);

            SetStatus("Undo.");
        }

        private void RedoButton_Click(object sender, RoutedEventArgs e)
        {
            if (_redoStack.Count == 0 || _currentImageBytes == null)
            {
                SetStatus("Nothing to redo.");
                return;
            }

            _undoStack.Push(_currentImageBytes);

            _currentImageBytes = _redoStack.Pop();
            ShowResult(_currentImageBytes);

            SetStatus("Redo.");
        }

        private void ApplyZoom()
        {
            OriginalImage.LayoutTransform = new System.Windows.Media.ScaleTransform(_zoomFactor, _zoomFactor);
            ResultImage.LayoutTransform = new System.Windows.Media.ScaleTransform(_zoomFactor, _zoomFactor);

            SetStatus($"Zoom: {_zoomFactor:0.00}x");
        }

        private void ZoomInButton_Click(object sender, RoutedEventArgs e)
        {
            _zoomFactor += 0.25;

            if (_zoomFactor > 4.0)
                _zoomFactor = 4.0;

            ApplyZoom();
        }

        private void ZoomOutButton_Click(object sender, RoutedEventArgs e)
        {
            _zoomFactor -= 0.25;

            if (_zoomFactor < 0.25)
                _zoomFactor = 0.25;

            ApplyZoom();
        }

        private void ResetZoomButton_Click(object sender, RoutedEventArgs e)
        {
            _zoomFactor = 1.0;
            ApplyZoom();
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentImageBytes == null)
            {
                MessageBox.Show("Nema slike za čuvanje.");
                return;
            }

            SaveFileDialog dialog = new()
            {
                Filter = "PNG image|*.png",
                FileName = "result.png"
            };

            if (dialog.ShowDialog() != true)
                return;

            await File.WriteAllBytesAsync(dialog.FileName, _currentImageBytes);

            SetStatus("Saved result.");
        }

        private async void ExportMsiButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentImageBytes == null)
            {
                MessageBox.Show("Nema slike za export.");
                return;
            }

            try
            {
                Stopwatch stopwatch = Stopwatch.StartNew();

                SaveFileDialog dialog = new()
                {
                    Filter = "MSI image|*.msi",
                    FileName = "image.msi"
                };

                if (dialog.ShowDialog() != true)
                    return;

                SetStatus("Exporting MSI...");

                using MultipartFormDataContent form = new();

                form.Add(CreateImageContent(_currentImageBytes, "export.png"), "image", "export.png");
                string selectedCompression = ((System.Windows.Controls.ComboBoxItem)MsiCompressionComboBox.SelectedItem).Content.ToString()!;

                string compressionValue = selectedCompression.StartsWith("4") ? "4" : "2";

                form.Add(new StringContent(compressionValue), "compression");

                HttpResponseMessage response = await _httpClient.PostAsync($"{ServerBaseUrl}/export-msi", form);

                if (!response.IsSuccessStatusCode)
                {
                    string error = await response.Content.ReadAsStringAsync();
                    throw new InvalidOperationException(error);
                }

                byte[] msiBytes = await response.Content.ReadAsByteArrayAsync();

                await File.WriteAllBytesAsync(dialog.FileName, msiBytes);

                SetStatus($"MSI exported. Compression: {compressionValue}");

                stopwatch.Stop();

                await _logger.InfoAsync(
                    CurrentUser,
                    "ExportMSI",
                    $"Compression={compressionValue}",
                    stopwatch.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                await _logger.ErrorAsync(
                    CurrentUser,
                    "ExportMSI",
                    "MSI export failed",
                    ex);

                MessageBox.Show(ex.Message, "MSI export error", MessageBoxButton.OK, MessageBoxImage.Error);
                SetStatus("MSI export failed.");
            }
        }
    }
}