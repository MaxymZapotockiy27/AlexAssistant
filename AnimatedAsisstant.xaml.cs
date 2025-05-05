using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO; using System.Linq;
using System.Reflection; using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace AlexAssistant
{
                public partial class AnimatedAsisstant : Window
    {
        private List<BitmapSource> frames = new List<BitmapSource>();
        private int currentFrame = 0;
        private bool isAnimationRunning = false;
        private bool isAudioRunning = false;

        private DateTime lastFrameTime;
        private TimeSpan frameInterval = TimeSpan.FromMilliseconds(41.67); 
        private WaveInEvent? waveIn;
        private const double MinScale = 1.0;
        private const double MaxScale = 1.7;
        private const double SmoothingFactor = 0.25;
        private double targetScale = MinScale;
        private double currentScale = MinScale;

                                        private const string RelativeAnimationPath = "Animations\\AnimationAssitant"; 
        public AnimatedAsisstant()
        {
            InitializeComponent();
            LoadFrames(); 
            if (frames.Count > 0)
            {
                MyImage.Source = frames[0];
            }
            else
            {
                                            }

            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            PositionTopRight();
            StartAnimationAndAudio();
        }

        private void PositionTopRight()
        {
            if (!IsLoaded) return;
            this.UpdateLayout();
            var screen = SystemParameters.WorkArea;
            this.Left = screen.Right - this.ActualWidth;
            this.Top = screen.Top;
        }

        private void LoadFrames()
        {
            List<BitmapSource> loadedFrames = new List<BitmapSource>();

            try
            {
                                string? exePath = System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                if (string.IsNullOrEmpty(exePath))
                {
                    MessageBox.Show("The path to the application folder could not be determined", "Critical error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                                string animationDirectory = System.IO.Path.Combine(exePath, RelativeAnimationPath);

                Debug.WriteLine($"Searching for frames in a folder: {animationDirectory}");

                                if (!Directory.Exists(animationDirectory))
                {
                    MessageBox.Show($"The animation folder was not found at the path: {animationDirectory}\n Check if the folder\r\n '{RelativeAnimationPath}'copied to the build directory (Build Action: Content, Copy to Output Directory: Copy if newer/always).", "Loading  Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                for (int i = 1; i <= 48; i++)                 {
                    string fileNameOnly = $"{i:D4}.png";                     string fullPath = System.IO.Path.Combine(animationDirectory, fileNameOnly);

                                        if (!File.Exists(fullPath))
                    {
                        Debug.WriteLine($"Warning: Frame file not found: {fullPath}");
                        continue;                     }

                    try
                    {
                                                var image = new BitmapImage();
                        image.BeginInit();
                                                image.UriSource = new Uri(fullPath, UriKind.Absolute);
                                                image.CacheOption = BitmapCacheOption.OnLoad;
                        image.CreateOptions = BitmapCreateOptions.PreservePixelFormat | BitmapCreateOptions.IgnoreColorProfile;
                        image.EndInit();

                                                if (image.CanFreeze)
                        {
                            image.Freeze();
                        }
                        loadedFrames.Add(image);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Frame loading error  {fileNameOnly} from  {fullPath}: {ex.Message}");
                                            }
                }
            }
            catch (Exception ex)             {
                Debug.WriteLine($"Critical error while initializing frame loading: {ex.Message}");
                MessageBox.Show($"An unexpected error occurred while trying to find the animation folder: {ex.Message}", "Critical error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;             }


                        if (loadedFrames.Count == 0)
            {
                MessageBox.Show($"Could not load any frames from the folder'{RelativeAnimationPath}'. Check for files (0001.png - 0048.png) and their availability.", "Error loading frames", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            else if (loadedFrames.Count < 48)
            {
                MessageBox.Show($"Only loaded {loadedFrames.Count} of 48 expected frames from the folder '{RelativeAnimationPath}'.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

                        if (loadedFrames.Any())
            {
                OptimizeFrames(loadedFrames);
            }
            else
            {
                frames.Clear();             }
        }

                private void OptimizeFrames(List<BitmapSource> sourceFrames)
        {
            frames.Clear(); 
            foreach (var frame in sourceFrames)
            {
                                                                if (frame.IsFrozen)                 {
                    frames.Add(frame);
                }
                else
                {
                                        if (frame.CanFreeze)
                    {
                        frame.Freeze();
                        frames.Add(frame);
                        Debug.WriteLine("Additionally, a frame is frozen in OptimizeFrames.");
                    }
                    else
                    {
                                                                        Debug.WriteLine("Warning: Failed to freeze frame in OptimizeFrames.");
                                                                        frames.Add(frame);                     }
                }

                                /*
                RenderTargetBitmap renderBitmap = new RenderTargetBitmap(
                    frame.PixelWidth, frame.PixelHeight, 96, 96, PixelFormats.Pbgra32);

                DrawingVisual drawingVisual = new DrawingVisual();
                using (DrawingContext dc = drawingVisual.RenderOpen())
                {
                    dc.DrawImage(frame, new Rect(0, 0, frame.PixelWidth, frame.PixelHeight));
                }
                renderBitmap.Render(drawingVisual);

                if (renderBitmap.CanFreeze)
                {
                    renderBitmap.Freeze();
                }
                frames.Add(renderBitmap);
                */
            }
                        Debug.WriteLine($"Optimized/added {frames.Count} frames.");
        }


                                
        private void StartAnimationAndAudio()
        {
            StartAnimation();
            StartAudioCapture();
        }

        private void StopAnimationAndAudio()
        {
            StopAnimation();
            StopAudioCapture();
        }

        private void StartAnimation()
        {
            if (isAnimationRunning || frames.Count == 0) return;

            CompositionTarget.Rendering -= CompositionTarget_Rendering;
            CompositionTarget.Rendering += CompositionTarget_Rendering;
            lastFrameTime = DateTime.Now;
            isAnimationRunning = true;
            Debug.WriteLine("Animation started");
        }

        private void StopAnimation()
        {
            if (!isAnimationRunning) return;

            CompositionTarget.Rendering -= CompositionTarget_Rendering;
            isAnimationRunning = false;
            Debug.WriteLine("Animation stopped");
        }

        private void CompositionTarget_Rendering(object? sender, EventArgs e)
        {
            if (!isAnimationRunning || frames.Count == 0) return;

            TimeSpan elapsed = DateTime.Now - lastFrameTime;
            if (elapsed >= frameInterval)
            {
                if (frames.Count > 0)                 {
                    currentFrame = (currentFrame + 1) % frames.Count;
                    MyImage.Source = frames[currentFrame];
                }
                lastFrameTime = DateTime.Now;
            }

            currentScale = currentScale + (targetScale - currentScale) * SmoothingFactor;

            imageScaleTransform.ScaleX = currentScale;
            imageScaleTransform.ScaleY = currentScale;
        }

        private void StartAudioCapture()
        {
            if (isAudioRunning || waveIn != null) return;

            try
            {
                if (WaveInEvent.DeviceCount == 0)
                {
                    Debug.WriteLine("Error: No recording devices available.");
                                                            return;
                }

                waveIn = new WaveInEvent
                {
                    DeviceNumber = 0,
                    WaveFormat = new WaveFormat(rate: 44100, bits: 16, channels: 1),
                    BufferMilliseconds = 50
                };
                waveIn.DataAvailable += WaveIn_DataAvailable;
                waveIn.RecordingStopped += WaveIn_RecordingStopped;

                waveIn.StartRecording();
                isAudioRunning = true;
                Debug.WriteLine("Audio capture started");
            }
            catch (NAudio.MmException mmEx)
            {
                Debug.WriteLine($"NAudio Audio startup error ({mmEx.Result}): {mmEx.Message}");
                                CleanUpAudio();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"General audio startup error: {ex.Message}");
                                CleanUpAudio();
            }
        }

        private void StopAudioCapture()
        {
            if (!isAudioRunning || waveIn == null) return;

            Debug.WriteLine("Request to stop audio capture...");
            try
            {
                waveIn?.StopRecording();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error when calling StopRecording:{ex.Message}");
                CleanUpAudio();
            }
        }

        private void WaveIn_DataAvailable(object? sender, WaveInEventArgs e)
        {
            if (e.BytesRecorded == 0 || waveIn == null || !isAudioRunning) return;

            float maxSample = 0;
            for (int index = 0; index < e.BytesRecorded; index += 2)
            {
                if (index + 1 < e.Buffer.Length)                 {
                    short sample = (short)((e.Buffer[index + 1] << 8) | e.Buffer[index]);
                    float sample32 = Math.Abs(sample / 32768f);
                    if (sample32 > maxSample)
                    {
                        maxSample = sample32;
                    }
                }
            }

            double level = Math.Sqrt(maxSample);
            targetScale = MinScale + (MaxScale - MinScale) * level;
            targetScale = Math.Max(MinScale, Math.Min(MaxScale, targetScale));         }

        private void WaveIn_RecordingStopped(object? sender, StoppedEventArgs e)
        {
            Debug.WriteLine("Handler WaveIn_RecordingStopped caused.");
            CleanUpAudio();             targetScale = MinScale; 
            if (e.Exception != null)
            {
                Debug.WriteLine($"Error while recording or stopping audio: {e.Exception.Message}");
                            }
            else
            {
                Debug.WriteLine("Audio capture is stopped normally.");
            }
        }

        private void CleanUpAudio()
        {
            if (waveIn == null) return;
            Debug.WriteLine("Clearing audio resources...");

            waveIn.DataAvailable -= WaveIn_DataAvailable;
            waveIn.RecordingStopped -= WaveIn_RecordingStopped;

            try { waveIn.Dispose(); }
            catch (Exception ex) { Debug.WriteLine($"Error during Dispose WaveInEvent: {ex.Message}"); }
            finally
            {
                waveIn = null;
                isAudioRunning = false;
                Debug.WriteLine("Audio resources cleared.");
            }
        }

        public void SetSpeed(double framesPerSecond)
        {
            if (framesPerSecond <= 0) return;
            frameInterval = TimeSpan.FromSeconds(1.0 / framesPerSecond);
            Debug.WriteLine($"Animation speed set: {framesPerSecond} FPS");
        }

        protected override void OnClosed(EventArgs e)
        {
            Debug.WriteLine("The window is closing....");
            StopAnimationAndAudio();
            base.OnClosed(e);
            Debug.WriteLine("The window is closed.");
        }
    }
}