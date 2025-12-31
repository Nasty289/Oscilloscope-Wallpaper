using NAudio.Wave;
using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace XYScope
{
    public partial class MainWindow : Window
    {
        WasapiLoopbackCapture capture;
        float[] buffer = new float[4096];

        WriteableBitmap bitmap;
        int width, height;
        byte[] pixelData;

        Point? previousPoint = null; // last point for connecting lines

        public MainWindow()
        {
            InitializeComponent();

            Loaded += (s, e) =>
            {
                width = (int)ScopeCanvas.ActualWidth;
                height = (int)ScopeCanvas.ActualHeight;
                bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
                pixelData = new byte[width * height * 4];

                var image = new System.Windows.Controls.Image { Source = bitmap };
                ScopeCanvas.Children.Add(image);
            };

            StartAudio();
            StartRenderLoop();
        }

        void StartAudio()
        {
            capture = new WasapiLoopbackCapture();
            capture.DataAvailable += OnAudio;
            capture.StartRecording();
        }

        void OnAudio(object sender, WaveInEventArgs e)
        {
            int samples = e.BytesRecorded / 4;
            if (samples > buffer.Length) samples = buffer.Length;
            Buffer.BlockCopy(e.Buffer, 0, buffer, 0, samples * 4);
        }

        void StartRenderLoop()
        {
            DispatcherTimer timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(16) // ~60 FPS
            };
            timer.Tick += (s, e) => Draw();
            timer.Start();
        }

        void Draw()
        {
            if (bitmap == null) return;

            // Fade previous frame (phosphor discharge)
            for (int i = 0; i < pixelData.Length; i += 4)
            {
                pixelData[i + 0] = (byte)(pixelData[i + 0] * 0.72); // B
                pixelData[i + 1] = (byte)(pixelData[i + 1] * 0.72); // G
                pixelData[i + 2] = (byte)(pixelData[i + 2] * 0.72); // R
            }

            double cx = width / 2.0;
            double cy = height / 2.0;
            double scale = Math.Min(cx, cy) * 2.0; // uniform scale

            previousPoint = null;

            for (int i = 0; i < buffer.Length - 1; i += 2)
            {
                float left = buffer[i] * 1.5f;
                float right = buffer[i + 1] * 1.5f;

                int x = (int)(cx + left * scale);
                int y = (int)(cy + right * scale);

                if (x < 1 || x >= width - 1 || y < 1 || y >= height - 1) continue;

                if (previousPoint != null)
                {
                    DrawLineFaint(previousPoint.Value, new Point(x, y));
                }

                previousPoint = new Point(x, y);
            }

            bitmap.WritePixels(new Int32Rect(0, 0, width, height), pixelData, width * 4, 0);
        }

        // Draw faint green line with subtle halo
        void DrawLineFaint(Point p0, Point p1)
        {
            int x0 = (int)p0.X, y0 = (int)p0.Y;
            int x1 = (int)p1.X, y1 = (int)p1.Y;

            int dx = Math.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
            int dy = -Math.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
            int err = dx + dy;

            while (true)
            {
                ApplyFaintPixel(x0, y0);

                if (x0 == x1 && y0 == y1) break;
                int e2 = 2 * err;
                if (e2 >= dy) { err += dy; x0 += sx; }
                if (e2 <= dx) { err += dx; y0 += sy; }
            }
        }

        // Apply faint green with subtle halo
        void ApplyFaintPixel(int x, int y)
        {
            int[,] halo = {
                {0, 10, 0},
                {10, 120, 10},
                {0, 10, 0}
            };

            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    int px = x + dx, py = y + dy;
                    if (px < 0 || px >= width || py < 0 || py >= height) continue;

                    int index = (py * width + px) * 4;
                    int g = halo[dy + 1, dx + 1];

                    pixelData[index + 1] = (byte)Math.Min(255, pixelData[index + 1] + g); // green
                    pixelData[index + 0] = (byte)Math.Min(255, pixelData[index + 0] + g / 6); // blue
                    pixelData[index + 2] = (byte)Math.Min(255, pixelData[index + 2] + g / 6); // red
                    pixelData[index + 3] = 255; // alpha
                }
            }
        }
    }
}
