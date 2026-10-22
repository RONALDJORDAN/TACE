using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace PrototipoConsole;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var window = new MainWindow();

        if (e.Args.Contains("--screenshot"))
        {
            window.WindowStartupLocation = WindowStartupLocation.Manual;
            window.Left = 0;
            window.Top = 0;
            window.Show();
            window.UpdateLayout();

            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            await Task.Delay(250);

            var root = (FrameworkElement)window.Content;
            int width = Math.Max(1, (int)Math.Ceiling(root.ActualWidth));
            int height = Math.Max(1, (int)Math.Ceiling(root.ActualHeight));

            var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(root);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));

            string output = e.Args.Length > 1
                ? Path.GetFullPath(e.Args[1])
                : Path.Combine(AppContext.BaseDirectory, "preview.png");

            using (var stream = File.Create(output))
            {
                encoder.Save(stream);
            }

            Shutdown();
            return;
        }

        window.Show();
    }
}
