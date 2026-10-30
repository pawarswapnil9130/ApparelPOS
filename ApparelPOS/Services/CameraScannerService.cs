using System.IO;
using System.Media;
using System.Runtime.InteropServices;
using System.Windows.Media.Imaging;
using OpenCvSharp;
using ZXing;
using ZXing.Common;

namespace ApparelPOS.Services;

public class CameraScannerService : ICameraScannerService, IDisposable
{
    private VideoCapture? _capture;
    private CancellationTokenSource? _cts;
    private bool _isRunning;
    private string _lastScannedBarcode = string.Empty;
    private DateTime _lastScanTime = DateTime.MinValue;

    public bool IsRunning => _isRunning;

    public event EventHandler<BitmapSource>? FrameCaptured;
    public event EventHandler<string>? BarcodeDetected;
    public event EventHandler<string>? StatusChanged;

    public bool Start(int cameraIndex = 0)
    {
        if (_isRunning) return true;

        try
        {
            _cts = new CancellationTokenSource();
            _isRunning = true;
            StatusChanged?.Invoke(this, "Initializing laptop camera...");

            Task.Run(() => CaptureLoop(cameraIndex, _cts.Token));
            return true;
        }
        catch (Exception ex)
        {
            _isRunning = false;
            StatusChanged?.Invoke(this, $"Camera error: {ex.Message}");
            return false;
        }
    }

    public void Stop()
    {
        _isRunning = false;
        try
        {
            _cts?.Cancel();
        }
        catch { }

        try
        {
            _capture?.Release();
            _capture?.Dispose();
            _capture = null;
        }
        catch { }

        StatusChanged?.Invoke(this, "Camera stopped.");
    }

    private void CaptureLoop(int cameraIndex, CancellationToken token)
    {
        try
        {
            _capture = new VideoCapture(cameraIndex, VideoCaptureAPIs.DSHOW);
            if (!_capture.IsOpened())
            {
                // Fallback to default API if DSHOW fails
                _capture.Dispose();
                _capture = new VideoCapture(cameraIndex);
            }

            if (!_capture.IsOpened())
            {
                _isRunning = false;
                StatusChanged?.Invoke(this, "Cannot access webcam. Ensure no other application (Zoom, Teams, Camera) is using it.");
                return;
            }

            // Set reasonable resolution for snappy barcode decoding
            _capture.Set(VideoCaptureProperties.FrameWidth, 640);
            _capture.Set(VideoCaptureProperties.FrameHeight, 480);

            StatusChanged?.Invoke(this, "Webcam active. Point garment barcode tag at camera.");

            var reader = new BarcodeReaderGeneric
            {
                AutoRotate = true,
                Options = new DecodingOptions
                {
                    TryHarder = true,
                    PossibleFormats = new List<BarcodeFormat>
                    {
                        BarcodeFormat.CODE_128,
                        BarcodeFormat.EAN_13,
                        BarcodeFormat.EAN_8,
                        BarcodeFormat.UPC_A,
                        BarcodeFormat.UPC_E,
                        BarcodeFormat.CODE_39,
                        BarcodeFormat.QR_CODE
                    }
                }
            };

            using var mat = new Mat();

            while (_isRunning && !token.IsCancellationRequested)
            {
                if (!_capture.Read(mat) || mat.Empty())
                {
                    Thread.Sleep(20);
                    continue;
                }

                // 1. Convert OpenCV Mat to WPF BitmapSource
                try
                {
                    var bmpSource = MatToBitmapSource(mat);
                    FrameCaptured?.Invoke(this, bmpSource);
                }
                catch { }

                // 2. Scan Barcode using ZXing RGBLuminanceSource
                try
                {
                    int totalBytes = mat.Width * mat.Height * 3;
                    byte[] rgbBytes = new byte[totalBytes];
                    Marshal.Copy(mat.Data, rgbBytes, 0, totalBytes);

                    var luminance = new RGBLuminanceSource(rgbBytes, mat.Width, mat.Height, RGBLuminanceSource.BitmapFormat.BGR24);
                    var result = reader.Decode(luminance);

                    if (result != null && !string.IsNullOrWhiteSpace(result.Text))
                    {
                        var text = result.Text.Trim();
                        // 1.8 second debouncing per identical code
                        if (text != _lastScannedBarcode || (DateTime.Now - _lastScanTime).TotalSeconds > 1.8)
                        {
                            _lastScannedBarcode = text;
                            _lastScanTime = DateTime.Now;

                            try
                            {
                                SystemSounds.Beep.Play();
                            }
                            catch { }

                            BarcodeDetected?.Invoke(this, text);
                        }
                    }
                }
                catch { }

                Thread.Sleep(33); // ~30 fps
            }
        }
        catch (Exception ex)
        {
            StatusChanged?.Invoke(this, $"Camera streaming error: {ex.Message}");
        }
        finally
        {
            _isRunning = false;
            try
            {
                _capture?.Release();
                _capture?.Dispose();
                _capture = null;
            }
            catch { }
        }
    }

    private static BitmapSource MatToBitmapSource(Mat mat)
    {
        Cv2.ImEncode(".bmp", mat, out byte[] buf);
        using var stream = new MemoryStream(buf);
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.StreamSource = stream;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    public void Dispose()
    {
        Stop();
    }
}
