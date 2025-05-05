using OpenCvSharp; // Main OpenCV library
using OpenCvSharp.WpfExtensions; // For WriteableBitmap()
using System;
using System.IO;
using System.Threading; // For CancellationTokenSource
using System.Threading.Tasks; // For Task
using System.Windows; // For base WPF classes
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Reflection; // To get assembly location

namespace AlexAssistant
{
    public partial class ModelTrainingPage : Page
    {
        private VideoCapture? capture;
        private Mat? frame;
        private WriteableBitmap? wb;
        private CancellationTokenSource? cancellationTokenSource;
        private Task? cameraTask;
        private MainWindow _mainWindow;
        private DispatcherTimer? countdownTimer;
        private int countdownValue;
        private string saveFolderPath = "";
        private bool isCapturingActive = false;
        private const int CaptureDurationSeconds = 30;
        private const int CameraDeviceIndex = 0; // Use 0 for default camera

        // --- Face Detection ---
        private CascadeClassifier? faceCascade;
        static readonly string FaceCascadeFilename = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "HaarCascades", "haarcascade_frontalface_default.xml");
        private static bool _savePathErrorLogged = false;
        private static bool _cascadeLoadErrorLogged = false; // Flag for cascade load error

        public ModelTrainingPage(MainWindow mainWindow)
        {
            _mainWindow = mainWindow;
            InitializeComponent();
            SetupSaveFolder();
            SetupCountdownTimer();
            // Load cascade here or in Page_Loaded
            LoadFaceCascade();
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            // Only initialize camera if cascade loaded successfully
            if (faceCascade != null && !faceCascade.Empty())
            {
                InitializeCamera();
            }
            else if (!_cascadeLoadErrorLogged) // Log error if not already logged
            {
                StatusTextBlock.Text = $"Error: Failed to load {FaceCascadeFilename}. Face detection disabled.";
                StartButton.IsEnabled = false;
                _cascadeLoadErrorLogged = true; // Prevent repeated logging
                MessageBox.Show($"Could not load the face detection model ({FaceCascadeFilename}).\n\nPlease ensure the file exists in the application's directory:\n{GetCascadeFilePath()}",
                                "Face Detection Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        private async void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            await StopCameraAsync(); // Includes cascade disposal now
            countdownTimer?.Stop();
            Console.WriteLine("Page unloaded, resources released.");
        }

        // Helper to get the expected cascade file path
        private string GetCascadeFilePath()
        {
            try
            {
                // Attempt 1: Application Base Directory (works for deployed apps)
                string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
                string cascadePath = Path.Combine(baseDirectory, FaceCascadeFilename);
                if (File.Exists(cascadePath)) return cascadePath;

                // Attempt 2: Executing Assembly Location (might differ in some scenarios)
                string? assemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                if (!string.IsNullOrEmpty(assemblyLocation))
                {
                    cascadePath = Path.Combine(assemblyLocation, FaceCascadeFilename);
                    if (File.Exists(cascadePath)) return cascadePath;
                }

                // Fallback: just return the filename if paths dont exist (indicates it wasn't copied)
                return FaceCascadeFilename;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting cascade file path: {ex.Message}");
                return FaceCascadeFilename; // Fallback
            }
        }

        // Load the Haar Cascade file for face detection
        private void LoadFaceCascade()
        {
            string cascadePath = GetCascadeFilePath();
            try
            {
                if (!File.Exists(cascadePath))
                {
                    throw new FileNotFoundException($"Haar Cascade file not found at: {cascadePath}. Make sure it's set to 'Copy to Output Directory' in Visual Studio.");
                }
                faceCascade = new CascadeClassifier(cascadePath);
                if (faceCascade.Empty())
                {
                    throw new ApplicationException($"Failed to load cascade classifier from: {cascadePath}. File might be corrupt or invalid.");
                }
                Console.WriteLine("Face cascade loaded successfully.");
                _cascadeLoadErrorLogged = false; // Reset flag on success
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading face cascade: {ex.Message}");
                faceCascade?.Dispose(); // Dispose if partially created
                faceCascade = null;
                // UI feedback will happen in Page_Loaded
            }
        }

        private void InitializeCamera()
        {
            // Check again if cascade is loaded before proceeding
            if (faceCascade == null || faceCascade.Empty())
            {
                StatusTextBlock.Text = "Face detection model not loaded. Camera cannot start.";
                StartButton.IsEnabled = false;
                return;
            }

            StatusTextBlock.Text = "Initializing camera...";
            InstructionTextBlock.Visibility = Visibility.Collapsed;
            StartButton.IsEnabled = false;

            try
            {
                capture = new VideoCapture(CameraDeviceIndex, VideoCaptureAPIs.DSHOW);
                frame = new Mat();

                if (!capture.IsOpened())
                {
                    throw new ApplicationException("Failed to open webcam. Check connection, drivers, and permissions.");
                }

                int frameWidth = (int)capture.Get(VideoCaptureProperties.FrameWidth);
                int frameHeight = (int)capture.Get(VideoCaptureProperties.FrameHeight);
                Console.WriteLine($"Camera opened. Frame size: {frameWidth}x{frameHeight}");

                cancellationTokenSource = new CancellationTokenSource();
                var token = cancellationTokenSource.Token;

                cameraTask = Task.Run(() => CaptureCameraCallback(token), token);

                StatusTextBlock.Text = "Camera active. Position your face within the outline.";
                InstructionTextBlock.Text = "When recording starts, slowly look left, right, up, and down.";
                InstructionTextBlock.Visibility = Visibility.Visible;
                StartButton.IsEnabled = true;
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = $"Camera error: {ex.Message}";
                MessageBox.Show($"Camera initialization error: {ex.Message}\n\nPerhaps Visual C++ Redistributable for Visual Studio 2015-2022 is required, or the camera is in use by another application.", "Camera Error", MessageBoxButton.OK, MessageBoxImage.Error);
                capture?.Dispose();
                frame?.Dispose();
                capture = null;
                frame = null;
            }
        }

        private async Task StopCameraAsync()
        {
            Console.WriteLine("Stopping camera...");
            isCapturingActive = false; // Ensure saving stops

            if (cancellationTokenSource != null && !cancellationTokenSource.IsCancellationRequested)
            {
                try { cancellationTokenSource.Cancel(); Console.WriteLine("Cancellation token sent."); }
                catch (ObjectDisposedException) { /* Ignore */ }
            }

            if (cameraTask != null)
            {
                try
                {
                    Console.WriteLine("Waiting for camera task completion...");
                    await Task.WhenAny(cameraTask, Task.Delay(1500));
                    Console.WriteLine($"Camera task status after waiting: {cameraTask.Status}");
                }
                catch (Exception ex) { Console.WriteLine($"Error while waiting for camera task stop: {ex.Message}"); }
                finally { cameraTask = null; }
            }

            try { capture?.Release(); capture?.Dispose(); Console.WriteLine("VideoCapture released."); }
            catch (Exception ex) { Console.WriteLine($"Error releasing VideoCapture: {ex.Message}"); }
            try { frame?.Dispose(); Console.WriteLine("Mat frame released."); }
            catch (Exception ex) { Console.WriteLine($"Error releasing Mat: {ex.Message}"); }
            try { cancellationTokenSource?.Dispose(); Console.WriteLine("CancellationTokenSource disposed."); }
            catch (Exception ex) { Console.WriteLine($"Error disposing CancellationTokenSource: {ex.Message}"); }

            // --- Dispose Face Cascade ---
            try { faceCascade?.Dispose(); Console.WriteLine("FaceCascade disposed."); }
            catch (Exception ex) { Console.WriteLine($"Error disposing FaceCascade: {ex.Message}"); }
            // --------------------------

            capture = null;
            frame = null;
            cancellationTokenSource = null;
            faceCascade = null; // Nullify reference

            try
            {
                if (Dispatcher != null)
                { await Dispatcher.InvokeAsync(() => VideoImage.Source = null, DispatcherPriority.Normal); }
            }
            catch (TaskCanceledException) { /* Ignore */ }

            Console.WriteLine("Camera stopping sequence complete.");
        }


        private void CaptureCameraCallback(CancellationToken token)
        {
            Console.WriteLine("Camera reading task started.");
            DateTime lastSaveTime = DateTime.MinValue;
            // Adjust save interval - face detection takes time, saving too often might be taxing
            TimeSpan saveInterval = TimeSpan.FromMilliseconds(200); // e.g., Save attempt up to 5 times/sec

            try
            {
                while (!token.IsCancellationRequested)
                {
                    // Check resources early in the loop
                    if (capture == null || !capture.IsOpened() || frame == null || frame.IsDisposed || faceCascade == null || faceCascade.Empty())
                    {
                        Console.WriteLine("Capture loop interrupted: Critical resource (Camera, frame, Cascade) is invalid.");
                        break;
                    }

                    bool success = capture.Read(frame);
                    if (!success || frame.Empty())
                    {
                        Task.Delay(20, token).Wait(token);
                        continue;
                    }

                    // --- Save Frame Logic (Now with Face Detection) ---
                    if (isCapturingActive && (DateTime.Now - lastSaveTime) >= saveInterval)
                    {
                        // Pass a clone to the saving method to avoid race conditions with the main frame
                        SaveDetectedFaces(frame); // Pass the current frame for detection
                        lastSaveTime = DateTime.Now;
                    }

                    // --- Update UI Logic ---
                    Dispatcher.InvokeAsync(() =>
                    {
                        if (token.IsCancellationRequested || frame == null || frame.IsDisposed || frame.Empty())
                        { return; }

                        try
                        {
                            // Draw rectangles on the *display* frame for user feedback (optional)
                            Mat displayFrame = frame.Clone(); // Clone for drawing without affecting saved data
                            if (faceCascade != null && !faceCascade.Empty()) // Check cascade validity again
                            {
                                using (var gray = new Mat()) // Use using for temporary gray mat
                                {
                                    Cv2.CvtColor(displayFrame, gray, ColorConversionCodes.BGR2GRAY);
                                    Cv2.EqualizeHist(gray, gray); // Optional: helps detection sometimes
                                    // Use reasonable minSize to avoid tiny false positives
                                    OpenCvSharp.Rect[] faces = faceCascade.DetectMultiScale(gray, 1.1, 5, HaarDetectionTypes.ScaleImage, new OpenCvSharp.Size(60, 60));
                                    foreach (OpenCvSharp.Rect faceRect in faces)
                                    {
                                        Cv2.Rectangle(displayFrame, faceRect, Scalar.LimeGreen, 2); // Draw green rectangle
                                    }
                                } // gray Mat disposed here
                            }

                            // Convert the potentially annotated frame for display
                            wb = displayFrame.ToWriteableBitmap();
                            displayFrame.Dispose(); // Dispose the clone used for drawing

                            if (wb != null)
                            {
                                if (wb.CanFreeze) { wb.Freeze(); }
                                VideoImage.Source = wb;
                            }
                            else { Console.WriteLine("Error: displayFrame.ToWriteableBitmap() returned null."); }
                        }
                        catch (ObjectDisposedException ode) { Console.WriteLine($"Error accessing disposed object during UI update: {ode.ObjectName} - {ode.Message}"); }
                        catch (Exception ex) { Console.WriteLine($"Error converting or displaying frame: {ex.Message}"); }

                    }, DispatcherPriority.Render, token);
                }
            }
            catch (OperationCanceledException) { Console.WriteLine("Camera reading task cancelled."); }
            catch (Exception ex)
            {
                Console.WriteLine($"Critical error in camera task: {ex}\n{ex.StackTrace}");
                Dispatcher?.InvokeAsync(() => { StatusTextBlock.Text = "Critical camera error."; StartButton.IsEnabled = false; });
            }
            finally { Console.WriteLine("Camera reading task finished."); }
        }

        // Detects faces, processes them (grayscale, equalize), and saves them
        private void SaveDetectedFaces(Mat sourceFrame)
        {
            // --- Pre-checks ---
            if (faceCascade == null || faceCascade.Empty()) { /* Console.WriteLine("Save error: Face cascade not loaded."); */ return; } // Cascade check
            if (string.IsNullOrEmpty(saveFolderPath) || !Directory.Exists(saveFolderPath))
            {
                if (!_savePathErrorLogged) { Console.WriteLine("Save error: Invalid save path."); _savePathErrorLogged = true; }
                return;
            }
            _savePathErrorLogged = false; // Reset log flag if path is valid

            // Check source frame validity
            if (sourceFrame == null || sourceFrame.IsDisposed || sourceFrame.Empty()) { Console.WriteLine("Save error: Input frame is invalid."); return; }

            // --- Clone the frame immediately ---
            Mat? clonedFrame = null;
            try
            {
                clonedFrame = sourceFrame.Clone(); // Clone to work on without interference
                if (clonedFrame == null || clonedFrame.IsDisposed || clonedFrame.Empty())
                {
                    throw new Exception("Cloning resulted in an invalid Mat.");
                }
            }
            catch (Exception ex) { Console.WriteLine($"Error cloning frame for saving: {ex.Message}"); clonedFrame?.Dispose(); return; }


            // --- Perform detection and saving in a background task ---
            Task.Run(() =>
            {
                Mat? grayFrame = null; // Mat for grayscale conversion

                try
                {
                    // Convert the *cloned* frame to grayscale for detection
                    grayFrame = new Mat();
                    Cv2.CvtColor(clonedFrame, grayFrame, ColorConversionCodes.BGR2GRAY);

                    // Optional: Equalize histogram for potentially better detection/consistency
                    Cv2.EqualizeHist(grayFrame, grayFrame);

                    // Detect faces in the grayscale image
                    // Adjust parameters: scaleFactor, minNeighbors, minSize as needed
                    OpenCvSharp.Rect[] faces = faceCascade.DetectMultiScale(
                        image: grayFrame,
                        scaleFactor: 1.1,      // How much the image size is reduced at each image scale
                        minNeighbors: 5,       // How many neighbors each candidate rectangle should have
                        flags: HaarDetectionTypes.ScaleImage, // Standard flag
                        minSize: new OpenCvSharp.Size(60, 60) // Minimum possible face size (adjust based on distance)
                                                  // maxSize: new Size(300, 300) // Optional: Maximum possible face size
                    );

                    // If no faces detected, just exit the task for this frame
                    if (faces.Length == 0)
                    {
                        // Console.WriteLine("No faces detected in this frame."); // Optional log
                        return;
                    }

                    // --- Process and save each detected face ---
                    for (int i = 0; i < faces.Length; i++)
                    {
                        OpenCvSharp.Rect faceRect = faces[i];

                        // --- Crop the face from the grayscale image ---
                        Mat? faceROI = null; // ROI = Region of Interest
                        try
                        {
                            // Important: Create ROI from the grayFrame using the detected rectangle
                            faceROI = new Mat(grayFrame, faceRect);

                            // --- Save the processed face ROI ---
                            if (faceROI != null && !faceROI.IsDisposed && !faceROI.Empty())
                            {
                                // Generate unique filename
                                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
                                // Include face index if multiple faces are found in one frame
                                string fileName = Path.Combine(saveFolderPath, $"face_{timestamp}_{i}.jpg");

                                // Save as JPG (common for datasets, smaller size)
                                // You can adjust JPG quality (0-100, default 95)
                                bool saved = Cv2.ImWrite(fileName, faceROI, new ImageEncodingParam(ImwriteFlags.JpegQuality, 90));

                                if (!saved) { Console.WriteLine($"Error writing JPG file: {fileName}"); }
                                // else { Console.WriteLine($"Saved face: {fileName}"); } // Optional success log
                            }
                        }
                        catch (Exception ex) { Console.WriteLine($"Error processing/saving face ROI #{i}: {ex.Message}"); }
                        finally
                        {
                            faceROI?.Dispose(); // Dispose the ROI Mat explicitly
                        }

                        // Optional: Add a small delay if saving multiple faces from one frame to avoid identical timestamps
                        // if (faces.Length > 1) Task.Delay(5).Wait(); // Small delay
                    } // End foreach face
                }
                catch (Exception ex) { Console.WriteLine($"Error during face detection/saving task: {ex.Message}\n{ex.StackTrace}"); }
                finally
                {
                    // --- IMPORTANT: Dispose Mats created in this task ---
                    grayFrame?.Dispose();   // Dispose the grayscale version
                    clonedFrame?.Dispose(); // Dispose the clone we made at the start
                }
            }); // End Task.Run
        }

        // (SetupSaveFolder, SetupCountdownTimer, StartButton_Click, CountdownTimer_Tick, StopCaptureRecording, ChangeToTrainPage remain largely the same as your original code)
        // ... Make sure these methods are still present and correct ...
        private void SetupSaveFolder()
        {
            try
            {
                string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                saveFolderPath = Path.Combine(appDataPath, "AlexAssistant", "UserFaceData");

                if (!Directory.Exists(saveFolderPath))
                {
                    Directory.CreateDirectory(saveFolderPath);
                    Console.WriteLine($"Created save folder: {saveFolderPath}");
                }
                else { Console.WriteLine($"Save folder exists: {saveFolderPath}"); }
                _savePathErrorLogged = false; // Reset error log flag
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not create or access the folder '{saveFolderPath}': {ex.Message}", "Folder Access Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                saveFolderPath = ""; // Invalidate path
                _savePathErrorLogged = true; // Set error log flag
            }
        }

        private void SetupCountdownTimer()
        {
            countdownTimer = new DispatcherTimer();
            countdownTimer.Interval = TimeSpan.FromSeconds(1);
            countdownTimer.Tick += CountdownTimer_Tick;
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            if (capture == null || !capture.IsOpened()) { MessageBox.Show("Camera is not initialized or became unavailable.", "Camera Error", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            if (faceCascade == null || faceCascade.Empty()) { MessageBox.Show("Face detection model not loaded. Cannot start.", "Model Error", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            if (string.IsNullOrEmpty(saveFolderPath)) { MessageBox.Show($"The folder for saving photos is unavailable. Recording cannot start.", "Folder Error", MessageBoxButton.OK, MessageBoxImage.Warning); return; }

            isCapturingActive = true;
            StartButton.IsEnabled = false;
            countdownValue = CaptureDurationSeconds;
            TimerTextBlock.Text = $"{countdownValue} sec";
            StatusTextBlock.Text = "Recording... Slowly look left, right, up, down.";
            InstructionTextBlock.Visibility = Visibility.Collapsed;
            TimerTextBlock.Visibility = Visibility.Visible;
            CaptureProgress.Visibility = Visibility.Visible;
            CaptureProgress.Value = 0;
            CaptureProgress.Maximum = CaptureDurationSeconds;
            countdownTimer?.Start();
            Console.WriteLine($"Recording started ({CaptureDurationSeconds} sec). Saving detected faces.");
        }

        private void CountdownTimer_Tick(object? sender, EventArgs e)
        {
            countdownValue--;
            TimerTextBlock.Text = $"{countdownValue} sec";
            CaptureProgress.Value = CaptureDurationSeconds - countdownValue;

            if (countdownValue <= 0)
            {
                // Stop recording *before* navigating away
                StopCaptureRecording();
                // Navigate after stopping ensures resources are handled cleanly before page switch
                ChangeToTrainPage();
            }
        }
        private void ChangeToTrainPage()
        {
            // Make sure navigation happens on the UI thread
            Dispatcher.Invoke(() => {
                _mainWindow.NavigateWithFade(new GenerateModel(_mainWindow));
            });
        }

        private void StopCaptureRecording()
        {
            if (!isCapturingActive && countdownTimer?.IsEnabled == false) return; // Avoid stopping multiple times

            isCapturingActive = false;
            countdownTimer?.Stop();
            Console.WriteLine("Recording finished.");

            if (this.IsLoaded) // Check if page is still loaded
            {
                // Use Dispatcher for UI updates from potentially different threads (like timer tick)
                Dispatcher.Invoke(() => {
                    StartButton.IsEnabled = true;
                    TimerTextBlock.Visibility = Visibility.Collapsed;
                    CaptureProgress.Visibility = Visibility.Collapsed;
                    string completionMessage = $"Complete! Face images saved.";
                    if (!string.IsNullOrEmpty(saveFolderPath))
                    {
                        // Getting folder name safely
                        try { completionMessage += $"\n(Folder: ...\\{Path.GetFileName(Path.GetDirectoryName(saveFolderPath))}\\{Path.GetFileName(saveFolderPath)})"; } catch { /* ignore path errors */ }
                    }
                    StatusTextBlock.Text = completionMessage;
                    InstructionTextBlock.Text = "When recording starts, slowly look left, right, up, and down.";
                    InstructionTextBlock.Visibility = Visibility.Visible;
                });
            }
        }

    }
}