using OpenCvSharp.Face;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Windows;
// Assuming AlexAssistant.Models exists and might be needed elsewhere
// using AlexAssistant.Models;

namespace AlexAssistant.Services
{
    public class FaceRecogService
    {
        static readonly string cascadeFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "HaarCascades", "haarcascade_frontalface_default.xml");
        static readonly string userFacesFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AlexAssistant", "UserFaceData");
        static readonly string modelFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "FaceModels", "UserFace.yml");

        static readonly CascadeClassifier faceCascade;
        static readonly LBPHFaceRecognizer recognizer;

        static int trainedPersonLabel = 1;
        static double confidenceThreshold = 80;
        static int consecutiveAbsenceFrames = 0;
        static DateTime lastDetectionTime;
        static bool imageViewerRunning = false;
        static Process imageViewerProcess = null;
        static string trainedPersonName = "User"; // Default or load this value
        static int absenceTimeThresholdSeconds = 5;
        static DateTime absenceStartTime;
        static bool personIsAbsent = false;
        static int framesRequiredForAbsence = 15;
        static bool modelTrained = false;
        private static CancellationTokenSource _cts;
        private static Task _recognizeTask;

        public static void Start()
        {
            if (_recognizeTask != null && !_recognizeTask.IsCompleted)
                return; 

            _cts = new CancellationTokenSource();
            _recognizeTask = Task.Run(() => RecognizeFace(_cts.Token));
        }
        public static void Stop()
        {
            if (_cts != null)
            {
                _cts.Cancel();
                _cts = null;
            }

            _recognizeTask = null;
        }

        static FaceRecogService()
        {
            try
            {
                if (!File.Exists(cascadeFilePath))
                {
                    Console.WriteLine($"Error: Cascade file not found at {cascadeFilePath}");
                    Console.WriteLine("Please download the haarcascade_frontalface_default.xml file and place it in the HaarCascades directory.");
                    throw new FileNotFoundException("HaarCascade file not found.", cascadeFilePath);
                }

                faceCascade = new CascadeClassifier(cascadeFilePath);
                recognizer = LBPHFaceRecognizer.Create();

                // Create necessary directories
                Directory.CreateDirectory(Path.GetDirectoryName(cascadeFilePath));
                Directory.CreateDirectory(userFacesFolder);
                Directory.CreateDirectory(Path.GetDirectoryName(modelFilePath));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing static members: {ex.Message}");
                throw;
            }
        }

        public FaceRecogService()
        {
            try
            {
                lastDetectionTime = DateTime.Now;
                absenceStartTime = DateTime.Now;

                // Try to load the model and check if it's valid
                if (!LoadModel())
                {
                    Console.WriteLine("Face model couldn't be loaded or is invalid. Model needs training.");
                    // If there are training images available, train the model immediately
                    string[] trainingImages = Directory.GetFiles(userFacesFolder, "*.jpg");
                    if (trainingImages.Length > 0)
                    {
                        Console.WriteLine($"Found {trainingImages.Length} training images. Training model now...");
                        TrainModel();
                    }
                    else
                    {
                        Console.WriteLine("No training images found. Model will remain untrained.");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing FaceRecogService: {ex.Message}");
                throw;
            }
        }

        public static bool LoadModel()
        {
            if (File.Exists(modelFilePath))
            {
                try
                {
                    FileInfo modelInfo = new FileInfo(modelFilePath);
                    if (modelInfo.Length <= 0)
                    {
                        Console.WriteLine("Model file exists but is empty. Needs training.");
                        return false;
                    }

                    recognizer.Read(modelFilePath);

                    // Verify the model is actually loaded and valid
                    try
                    {
                        // Try to access a property that would fail if model isn't loaded
                        // This is just a test - the result doesn't matter
                        var test = recognizer.GetThreshold();
                        modelTrained = true;
                        Console.WriteLine("Existing face recognition model loaded successfully.");
                        return true;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Model exists but appears to be invalid: {ex.Message}");
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error loading face recognition model: {ex.Message}");
                    return false;
                }
            }
            else
            {
                Console.WriteLine("No existing face recognition model found. Needs training.");
                return false;
            }
        }

        public static void TrainModel()
        {
            Directory.CreateDirectory(userFacesFolder);
            string[] imagePaths = Directory.GetFiles(userFacesFolder, "*.jpg");

            if (imagePaths.Length == 0)
            {
                Console.WriteLine("No images found in UserFaceData for training.");
                modelTrained = false;
                return;
            }

            Console.WriteLine($"Found {imagePaths.Length} training images. Training model...");
            var images = new List<Mat>();
            var labels = new List<int>();

            foreach (var imagePath in imagePaths)
            {
                using (var img = Cv2.ImRead(imagePath, ImreadModes.Grayscale))
                {
                    if (img == null || img.Empty())
                    {
                        Console.WriteLine($"Warning: Could not read or empty image: {imagePath}");
                        continue;
                    }
                    images.Add(img.Clone());
                    labels.Add(trainedPersonLabel);
                }
            }

            if (images.Count == 0)
            {
                Console.WriteLine("No valid images were loaded for training.");
                modelTrained = false;
                return;
            }

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(modelFilePath));
                recognizer.Train(images.ToArray(), labels.ToArray());
                recognizer.Write(modelFilePath);
                foreach (string file in Directory.GetFiles(userFacesFolder))
                {
                    File.Delete(file);
                }
                // Verify the model was properly trained
                if (!File.Exists(modelFilePath) || new FileInfo(modelFilePath).Length <= 0)
                {
                    Console.WriteLine("Warning: Model file was not created or is empty after training.");
                    modelTrained = false;
                    return;
                }

                modelTrained = true;
                Console.WriteLine($"Training complete. Model saved to {modelFilePath}");

                try
                {
                    // Uncomment this section if you want to delete training images after training
                    /*
                    foreach (var file in Directory.GetFiles(userFacesFolder))
                    {
                        File.Delete(file);
                    }

                    foreach (var dir in Directory.GetDirectories(userFacesFolder))
                    {
                        Directory.Delete(dir, true);
                    }

                    Console.WriteLine("All contents deleted.");
                    */
                }
                catch (UnauthorizedAccessException uae)
                {
                    Console.WriteLine($"Access error while deleting contents: {uae.Message}");
                }
                catch (IOException ioEx)
                {
                    Console.WriteLine($"I/O error while deleting contents: {ioEx.Message}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error deleting contents: {ex.Message}");
                }
            }
            catch (FileNotFoundException fnfEx)
            {
                Console.WriteLine($"File not found: {fnfEx.Message}");
                modelTrained = false;
            }
            catch (UnauthorizedAccessException uae)
            {
                Console.WriteLine($"Access error while saving model: {uae.Message}");
                modelTrained = false;
            }
            catch (IOException ioEx)
            {
                Console.WriteLine($"I/O error during training or saving model: {ioEx.Message}");
                modelTrained = false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unexpected error during training or saving model: {ex.Message}");
                modelTrained = false;
            }
            finally
            {
                foreach (var mat in images)
                {
                    mat?.Dispose();
                }
            }
        }

        public static async Task TrainModelAsync()
        {
            await Task.Run(() => {
                TrainModel();
            });
        }
        static FullScreenOverlayWindow? fullScreenOverlay = null;

        static void LauchOVerlayScreen()    
        {
            Console.WriteLine("Launching Overlay Screen");
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (fullScreenOverlay == null)
                {
                    fullScreenOverlay = new FullScreenOverlayWindow();
                    fullScreenOverlay.Show();
                }
            });

            imageViewerRunning = true;
        }

        static void CloseOVerlayScreen()
        {
            Console.WriteLine("Closing Overlay Screen");
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (fullScreenOverlay != null)
                {
                    fullScreenOverlay.Close();
                    fullScreenOverlay = null;
                }
            });

            imageViewerRunning = false;
        }

        public static void RecognizeFace(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    // Check if model is trained, and if not, try to train it
                    if (!modelTrained)
                    {
                        Console.WriteLine("Face model not trained. Attempting to load or train model...");
                        if (!LoadModel())
                        {
                            Console.WriteLine("Training new model...");
                            TrainModel();

                            // If still not trained, we can't proceed
                            if (!modelTrained)
                            {
                                Console.WriteLine("ERROR: Could not train face recognition model. Make sure training images exist.");
                                return;
                            }
                        }
                    }

                    using (var capture = new VideoCapture(0))
                    {
                        if (!capture.IsOpened())
                        {
                            Console.WriteLine("Error: Could not open webcam.");
                            return;
                        }

                        Console.WriteLine("Starting face recognition. Press ESC to exit.");

                        // Replace the infinite loop with one that checks cancellation
                        while (!token.IsCancellationRequested)
                        {
                            bool personDetectedInThisFrame = false;
                            using (var frame = new Mat())
                            {
                                // Check for cancellation before each significant operation
                                if (token.IsCancellationRequested)
                                {
                                    break;
                                }

                                if (!capture.Read(frame) || frame.Empty())
                                {
                                    Console.WriteLine("Warning: Could not read frame from webcam.");
                                    System.Threading.Thread.Sleep(50); // Avoid busy-waiting on error
                                    continue;
                                }

                                using (var grayFrame = new Mat())
                                {
                                    Cv2.CvtColor(frame, grayFrame, ColorConversionCodes.BGR2GRAY);
                                    Cv2.EqualizeHist(grayFrame, grayFrame);

                                    // Check for cancellation again
                                    if (token.IsCancellationRequested)
                                    {
                                        break;
                                    }

                                    OpenCvSharp.Rect[] faces = faceCascade.DetectMultiScale(grayFrame, 1.1, 5, HaarDetectionTypes.ScaleImage, new OpenCvSharp.Size(30, 30));

                                    foreach (var face in faces)
                                    {
                                        using (var faceROI = new Mat(grayFrame, face))
                                        {
                                            if (faceROI.Empty() || faceROI.Width <= 1 || faceROI.Height <= 1) continue;

                                            try
                                            {
                                                recognizer.Predict(faceROI, out int predictedLabel, out double confidence);

                                                bool isKnownPerson = predictedLabel == trainedPersonLabel && confidence < confidenceThreshold;

                                                string name = isKnownPerson
                                                    ? $"{trainedPersonName} ({confidence:F2})"
                                                    : $"Unknown ({confidence:F2})";
                                                Scalar rectColor = isKnownPerson ? Scalar.LimeGreen : Scalar.Red;

                                                Cv2.Rectangle(frame, face, rectColor, 2);
                                                Cv2.PutText(frame, name, new OpenCvSharp.Point(face.X, face.Y - 10), HersheyFonts.HersheyPlain, 1.2, rectColor, 2);

                                                if (isKnownPerson)
                                                {
                                                    personDetectedInThisFrame = true;
                                                    lastDetectionTime = DateTime.Now;
                                                    consecutiveAbsenceFrames = 0;

                                                    if (personIsAbsent)
                                                    {
                                                        personIsAbsent = false;
                                                        Console.WriteLine("Person returned to frame");
                                                        if (imageViewerRunning)
                                                        {
                                                            CloseOVerlayScreen();
                                                        }
                                                    }
                                                }
                                            }
                                            catch (OpenCvSharp.OpenCVException cvEx)
                                            {
                                                Console.WriteLine($"OpenCV Error during prediction: {cvEx.Message}");
                                                if (cvEx.Message.Contains("not computed"))
                                                {
                                                    Console.WriteLine("Model is not computed or invalid. Need to retrain.");
                                                    // If we reach here, something is wrong with the model
                                                    return;  // Exit recognition to allow retraining
                                                }
                                            }
                                            catch (Exception ex)
                                            {
                                                Console.WriteLine($"Error during face prediction: {ex.Message}");
                                            }
                                        }
                                    }
                                }

                                if (!personDetectedInThisFrame)
                                {
                                    consecutiveAbsenceFrames++;

                                    if (consecutiveAbsenceFrames == 1) // Start timer only on the first frame of absence detection
                                    {
                                        absenceStartTime = DateTime.Now;
                                    }

                                    if (consecutiveAbsenceFrames >= framesRequiredForAbsence && !personIsAbsent)
                                    {
                                        personIsAbsent = true;
                                        absenceStartTime = DateTime.Now; // Reset start time when officially absent
                                        Console.WriteLine("Person is now considered absent");
                                    }

                                    if (personIsAbsent)
                                    {
                                        TimeSpan absenceDuration = DateTime.Now - absenceStartTime;
                                        string timeMessage = $"Absence time: {absenceDuration.TotalSeconds:F1}s";
                                        Cv2.PutText(frame, timeMessage, new OpenCvSharp.Point(10, 30), HersheyFonts.HersheyPlain, 1.2, Scalar.Red, 2);

                                        if (absenceDuration.TotalSeconds > absenceTimeThresholdSeconds && !imageViewerRunning)
                                        {
                                            LauchOVerlayScreen();
                                        }
                                    }
                                }
                                else // Person detected, reset absence flag if necessary
                                {
                                    if (personIsAbsent)
                                    {
                                        personIsAbsent = false; // Already handled above, but ensures consistency
                                        Console.WriteLine("Person returned.");
                                        if (imageViewerRunning) CloseOVerlayScreen();
                                    }
                                    consecutiveAbsenceFrames = 0;
                                }
                            }

                            // Check for cancellation at the end of each loop
                            if (token.IsCancellationRequested)
                            {
                                break;
                            }
                        }
                    }

                    // If we get here due to cancellation, break out of the outer loop too
                    if (token.IsCancellationRequested)
                    {
                        break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // This is expected when cancellation is requested
                Console.WriteLine("Face recognition canceled.");
            }
            catch (Exception ex)
            {
                Console.WriteLine("❌ Error in FaceRecogService: " + ex.Message);
            }
            finally
            {
                // Clean up resources when stopping
                if (imageViewerRunning)
                {
                    CloseOVerlayScreen();
                }
                Console.WriteLine("Face recognition stopped.");
            }
        }
    }
}