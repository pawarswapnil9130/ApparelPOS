using System.Windows.Media.Imaging;

namespace ApparelPOS.Services;

public interface ICameraScannerService
{
    bool IsRunning { get; }
    event EventHandler<BitmapSource>? FrameCaptured;
    event EventHandler<string>? BarcodeDetected;
    event EventHandler<string>? StatusChanged;
    bool Start(int cameraIndex = 0);
    void Stop();
}
