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

        public MainWindow()
        {
            InitializeComponent();

            // Initialize bitmap after window is loaded
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

            // Fade previous frame (tracer effect)
            for (int i = 0; i < pixelData.Length; i += 4)
            {
                pixelData[i + 0] = (byte)(pixelData[i + 0] * 0.72); // B
                pixelData[i + 1] = (byte)(pixelData[i + 1] * 0.72); // G
                pixelData[i + 2] = (byte)(pixelData[i + 2] * 0.72); // R
                                                                    // Alpha stays 255
            }

            double cx = width / 2.0;
            double cy = height / 2.0;
            double scale = Math.Min(cx, cy) * 2.0;

            for (int i = 0; i < buffer.Length - 1; i += 2)
            {
                float left = buffer[i] * 1.5f;
                float right = buffer[i + 1] * 1.5f;

                int x = (int)(cx + left * scale);
                int y = (int)(cy + right * scale);

                if (x < 2 || x >= width - 2 || y < 2 || y >= height - 2) continue;

                // 5x5 glow kernel
                int[,] glow = {
            {  30,  60,  60,  60,  30 },
            {  60, 120, 150, 120,  60 },
            {  60, 150, 255, 150,  60 },
            {  60, 120, 150, 120,  60 },
            {  30,  60,  60,  60,  30 }
        };

                for (int dx = -2; dx <= 2; dx++)
                {
                    for (int dy = -2; dy <= 2; dy++)
                    {
                        SetPixelGlow(x + dx, y + dy, glow[dy + 2, dx + 2]);
                    }
                }
            }

            // Write bitmap
            bitmap.WritePixels(new Int32Rect(0, 0, width, height), pixelData, width * 4, 0);
        }

        // Adds green glow to existing pixel, max 255
        void SetPixelGlow(int x, int y, int g)
        {
            int index = (y * width + x) * 4;
            int newG = pixelData[index + 1] + g;
            pixelData[index + 1] = (byte)Math.Min(255, newG);
            // optional: also slightly increase center pixel R/B for subtle effect
            pixelData[index + 0] = (byte)Math.Min(255, pixelData[index + 0] + g / 4);
            pixelData[index + 2] = (byte)Math.Min(255, pixelData[index + 2] + g / 4);
            pixelData[index + 3] = 255; // alpha
        }

        void SetPixel(int x, int y, byte b, byte g, byte r)
        {
            int index = (y * width + x) * 4;
            pixelData[index + 0] = b;
            pixelData[index + 1] = g;
            pixelData[index + 2] = r;
            pixelData[index + 3] = 255; // Alpha
        }
    }
}
