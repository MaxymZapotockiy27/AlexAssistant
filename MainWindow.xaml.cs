using AlexAssistant.Services;  using System;
using System.Diagnostics;  using System.IO;           using System.Text;         using System.Threading;    using System.Threading.Tasks;  using System.Windows;        using System.Windows.Controls;  using System.Windows.Input;     using System.Windows.Media.Animation;  using Control;             using Grpc.Core;           using System.Collections.Generic;  using System.Linq;  using System.Windows.Forms;
using MessageBox = System.Windows.MessageBox;  using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.Net.Http;

namespace AlexAssistant
{
    public partial class MainWindow : Window
    {
        private Process? _pythonServerProcess;          private readonly CancellationTokenSource _serverClosingTokenSource = new CancellationTokenSource();          GrpcHost grpcHost = new GrpcHost();
        private string userfilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
         "AlexAssistant",
         "userData.json");
                 private const int DEPENDENCY_INSTALL_TIMEOUT_MS = 1800000;          private const int SERVER_START_TIMEOUT_MS = 60000;          private const int GRPC_CALL_TIMEOUT_SEC = 10;          private const int MAX_INSTALL_RETRIES = 3;          private volatile bool _isClosing = false;          private bool _pythonServerSuccessfullyStarted = false;  
        private NotifyIcon? _trayIcon;
        private bool _isReallyClosing = false;
        private List<BitmapSource> frames = new List<BitmapSource>();
        private int currentFrame = 0;
        private bool isAnimationRunning = false;
        private const double FramesPerSecond = 24;
        private TimeSpan frameInterval = TimeSpan.FromSeconds(1.0 / FramesPerSecond);
        private DateTime lastFrameTime;
        private const string AnimationResourceFolderPath = "/Animations/AnimationAssitant";
        private const int NumberOfFrames = 48;

        public MainWindow()
        {
            FileLogger.Initialize();              FileLogger.Log("MainWindow Constructor Started.");

            InitializeComponent();
            this.Closing += MainWindow_Closing;              LoadFramesFromResources();
            InitializeTrayIcon();

            FileLogger.Log("Starting LaunchAsync task...");
                                      Task.Run(() => LaunchAsync()).ContinueWith(t => {
                if (t.IsFaulted)
                {
                                         FileLogger.LogError("Unhandled exception in LaunchAsync continuation task", t.Exception?.InnerException);
                                         ShowErrorToUser($"Unexpected error during launch: {t.Exception?.InnerException?.Message}");
                }
                else
                {
                    FileLogger.Log("LaunchAsync task completed successfully.");
                }
            }, TaskScheduler.FromCurrentSynchronizationContext());  
            if (File.Exists(userfilePath))
            {
                FileLogger.Log($"User data file found at '{userfilePath}', starting GrpcHost.");
                try
                {
                    grpcHost.Start();                 }
                catch (Exception ex)
                {
                    FileLogger.LogError("Failed to start GrpcHost", ex);
                    ShowErrorToUser($"Failed to start GrpcHost: {ex.Message}");
                }
            }
            else
            {
                FileLogger.Log($"User data file not found at '{userfilePath}'.");
            }
            FileLogger.Log("MainWindow Constructor Finished.");
        }

        private void LoadFramesFromResources()
        {
            FileLogger.Log("Loading animation frames from resources...");
            frames.Clear();
            List<BitmapSource> loadedFrames = new List<BitmapSource>();
            bool errorOccurred = false;

            Dispatcher.Invoke(() => {
                for (int i = 1; i <= NumberOfFrames; i++)
                {
                    string fileName = $"{i:D4}.png";
                    string uriString = $"pack://application:,,,{AnimationResourceFolderPath}/{fileName}";

                    try
                    {
                        var image = new BitmapImage();
                        image.BeginInit();
                        image.UriSource = new Uri(uriString, UriKind.Absolute);
                        image.CacheOption = BitmapCacheOption.OnLoad;
                        image.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
                        image.EndInit();
                        if (image.CanFreeze) image.Freeze();
                        loadedFrames.Add(image);
                    }
                    catch (Exception ex)
                    {
                        FileLogger.LogError($"Error loading frame resource '{uriString}'", ex);
                        errorOccurred = true;
                    }
                }
            });

            if (errorOccurred || loadedFrames.Count < NumberOfFrames)
            {
                FileLogger.Log($"Warning: Loaded only {loadedFrames.Count} out of {NumberOfFrames} expected frames.");
                if (loadedFrames.Count == 0)
                {
                                         ShowErrorToUser("Animation frames could not be loaded. The loading indicator may not be working.");
                }
            }

            if (loadedFrames.Count > 0)
            {
                frames = loadedFrames;
                FileLogger.Log($"Successfully loaded {frames.Count} frames.");
            }
            else
            {
                FileLogger.LogError("Error: No frames were loaded from resources.");
            }
        }

        private void StartAnimation()
        {
            if (frames.Count == 0)
            {
                FileLogger.Log("Cannot start animation: No frames loaded.");
                return;
            }
            if (isAnimationRunning) return;

            if (AnimationImage != null)
            {
                AnimationImage.Source = frames[currentFrame];
            }

            CompositionTarget.Rendering -= CompositionTarget_Rendering;
            CompositionTarget.Rendering += CompositionTarget_Rendering;
            lastFrameTime = DateTime.Now;
            isAnimationRunning = true;
            FileLogger.Log("Frame Animation started.");
        }

        private void StopAnimation()
        {
            if (!isAnimationRunning) return;
            CompositionTarget.Rendering -= CompositionTarget_Rendering;
            isAnimationRunning = false;
            FileLogger.Log("Frame Animation stopped.");
        }

        private void CompositionTarget_Rendering(object? sender, EventArgs e)
        {
            if (!isAnimationRunning || frames.Count == 0) return;

            if (DateTime.Now - lastFrameTime >= frameInterval)
            {
                currentFrame = (currentFrame + 1) % frames.Count;
                if (AnimationImage != null)                      AnimationImage.Source = frames[currentFrame];
                lastFrameTime = DateTime.Now;
            }
        }

        private void InitializeTrayIcon()
        {
            FileLogger.Log("Initializing system tray icon...");
            try
            {
                string iconPath = Path.Combine(AppContext.BaseDirectory, "AppImages", "favicon (1).ico");
                if (!File.Exists(iconPath))
                {
                    FileLogger.LogError($"Tray icon file not found at '{iconPath}'");
                    ShowErrorToUser($"Tray icon file not found: {iconPath}");
                    return;                  }

                _trayIcon = new NotifyIcon
                {
                    Icon = new System.Drawing.Icon(iconPath),
                    Visible = true,
                    Text = "Alex Assistant"
                };

                var contextMenu = new System.Windows.Forms.ContextMenuStrip();
                var openMenuItem = new System.Windows.Forms.ToolStripMenuItem("Open");
                openMenuItem.Click += (s, e) => ShowWindow();
                contextMenu.Items.Add(openMenuItem);

                var exitMenuItem = new System.Windows.Forms.ToolStripMenuItem("Exit");
                exitMenuItem.Click += (s, e) => CloseApplication();
                contextMenu.Items.Add(exitMenuItem);

                _trayIcon.ContextMenuStrip = contextMenu;
                _trayIcon.DoubleClick += (s, e) => ShowWindow();
                FileLogger.Log("System tray icon initialized successfully.");
            }
            catch (Exception ex)
            {
                FileLogger.LogError("Failed to initialize system tray icon", ex);
                ShowErrorToUser($"Failed to initialize tray icon: {ex.Message}");
                _trayIcon = null;              }
        }

        private void ShowWindow()
        {
            FileLogger.Log("Showing main window from tray.");
            this.Show();
            this.WindowState = WindowState.Normal;
            this.Activate();
        }

        private void HideToTray()
        {
            FileLogger.Log("Hiding main window to tray.");
            this.Hide();
        }

        private void CloseApplication()
        {
            FileLogger.Log("CloseApplication called from tray menu. Initiating full shutdown.");
            _isReallyClosing = true;
            if (_trayIcon != null)
            {
                try
                {
                    _trayIcon.Visible = false;
                    _trayIcon.Dispose();
                }
                catch (Exception ex)
                {
                    FileLogger.LogError("Error disposing tray icon", ex);
                }
                _trayIcon = null;
            }
            Close();          }

        public void ShowLoading(string message = "Loading...")
        {
            Dispatcher.Invoke(() => {
                FileLogger.Log($"Showing loading indicator: \"{message}\"");
                LoadingText.Text = message;
                LoadingIndicator.Visibility = Visibility.Visible;
                StartAnimation();
            });
        }

        public void HideLoading()
        {
            Dispatcher.Invoke(() => {
                FileLogger.Log("Hiding loading indicator.");
                LoadingIndicator.Visibility = Visibility.Collapsed;
                StopAnimation();
            });
        }

        private async Task LaunchAsync()
        {
            FileLogger.Log("LaunchAsync sequence started.");
            bool userExists = false;

            Dispatcher.Invoke(() => ShowLoading("Initializing the program..."));

            try
            {
                                 FileLogger.Log("Attempting to start Python backend...");
                Dispatcher.Invoke(() => ShowLoading("Launching the backend..."));
                bool serverStarted = await StartPythonServerFromBackendAsync();

                if (!serverStarted)
                {
                    _pythonServerSuccessfullyStarted = false;
                    FileLogger.LogError("Critical error: Failed to start Python backend. Check previous logs.");
                    Dispatcher.Invoke(() => {
                                                ShowErrorToUser("Critical error: Unable to start the Python backend...");
                        HideLoading();
                                            });
                    return;                 }
                else
                {
                    _pythonServerSuccessfullyStarted = true;
                    FileLogger.Log("Python backend process started successfully. Waiting for gRPC server to bind port...");
                    Dispatcher.Invoke(() => ShowLoading("Connecting to the server..."));
                    await Task.Delay(3000);                     FileLogger.Log("Continuing WPF app launch sequence after delay.");
                }

                                FileLogger.Log("Loading user data and navigating...");
                Dispatcher.Invoke(() => ShowLoading("Downloading user data..."));

                Dispatcher.Invoke(() => {
                    var userDataManager = new UserService();                     try
                    {
                        userExists = userDataManager.UserDataExist();
                        FileLogger.Log($"User data exists: {userExists}");
                        if (userExists)
                        {
                            FileLogger.Log("Navigating to DashboardPage.");
                            MainFrame.Navigate(new DashboardPage(this));
                        }
                        else
                        {
                            FileLogger.Log("Navigating to FirstLaunch.");
                            MainFrame.Navigate(new FirstLaunch(this));
                        }
                    }
                    catch (Exception ex)
                    {
                        FileLogger.LogError("Error loading user data or navigating", ex);
                        ShowErrorToUser($"Failed to load user data or initial page: {ex.Message}");
                        try
                        {
                            FileLogger.Log("Attempting fallback navigation to FirstLaunch.");
                            MainFrame.Navigate(new FirstLaunch(this)); userExists = false;
                        }
                        catch (Exception navEx)
                        {
                            FileLogger.LogError("Fallback navigation also failed", navEx);
                           
                        }
                    }
                });

                                if (userExists && Properties.Settings.Default.SaveConfidence)
                {
                    FileLogger.Log("Attempting to start Face Recognition service...");
                    Dispatcher.Invoke(() => ShowLoading("Initializing face recognition..."));
                    string faceModelPath = Path.Combine(AppContext.BaseDirectory, @"FaceModels\UserFace.yml");
                    FileLogger.Log($"Checking for face model at: {faceModelPath}");
                    if (File.Exists(faceModelPath))
                    {
                        try
                        {
                            FileLogger.Log("Face model found. Starting FaceRecogService...");
                            FaceRecogService.Start();                             FileLogger.Log("FaceRecogService.Start() called.");
                        }
                        catch (Exception ex)
                        {
                            FileLogger.LogError("Failed to start Face Recognition service", ex);
                                                    }
                    }
                    else
                    {
                        FileLogger.Log("Face model file not found. Face Recognition will be disabled.");
                    }
                }
                else
                {
                    if (!userExists) FileLogger.Log("Skipping Face Recognition: User data not found.");
                    if (!Properties.Settings.Default.SaveConfidence) FileLogger.Log("Skipping Face Recognition: SaveConfidence is false in settings.");
                }

                                if (Properties.Settings.Default.AssistantEnabled && _pythonServerSuccessfullyStarted && userExists)
                {
                    FileLogger.Log("Attempting initial gRPC TurnOn call...");
                    Dispatcher.Invoke(() => ShowLoading("Activate the voice assistant..."));
                    await InitialTurnOnAsync();
                }
                else
                {
                    FileLogger.Log("Skipping initial TurnOn call due to:");
                    if (!Properties.Settings.Default.AssistantEnabled) FileLogger.Log(" - Assistant is disabled in settings.");
                    if (!_pythonServerSuccessfullyStarted) FileLogger.Log(" - Python server did not start successfully.");
                    if (!userExists) FileLogger.Log(" - User data not found.");
                }
            }
            catch (Exception ex)
            {
                FileLogger.LogError("Unexpected error during LaunchAsync sequence", ex);
                Dispatcher.Invoke(() => {
                    ShowErrorToUser($"Unexpected error during application launch: {ex.Message}");
                });
            }
            finally
            {
                Dispatcher.Invoke(HideLoading);
                FileLogger.Log("LaunchAsync sequence finished.");
            }
        }

        private async Task InitialTurnOnAsync()
        {
            try
            {
                FileLogger.Log("Inside InitialTurnOnAsync.");
                string microphoneName = Properties.Settings.Default.MicrophoneName;
                if (string.IsNullOrEmpty(microphoneName))
                {
                    FileLogger.Log("Microphone name not set in properties. Skipping initial TurnOn.");
                    return;
                }
                FileLogger.Log($"Target microphone name: {microphoneName}");

                var turnOnRequest = new TurnOnRequest { MicNameTarget = microphoneName };
                var options = new CallOptions(deadline: DateTime.UtcNow.AddSeconds(GRPC_CALL_TIMEOUT_SEC));

                int retryCount = 0;
                const int MAX_RETRIES = 3;
                FileLogger.Log($"Attempting TurnOn call (Max retries: {MAX_RETRIES})");

                while (retryCount < MAX_RETRIES)
                {
                    retryCount++;
                    FileLogger.Log($"TurnOn attempt {retryCount}/{MAX_RETRIES}...");
                    try
                    {
                        var response = await GrpcService.Instance.Client.TurnOnAsync(turnOnRequest, options);
                        FileLogger.Log($"Initial Assistant TurnOn response: {response.Message}");
                        return;                     }
                    catch (RpcException ex) when (ex.StatusCode == StatusCode.Unavailable || ex.StatusCode == StatusCode.DeadlineExceeded)
                    {
                        FileLogger.Log($"TurnOn attempt {retryCount} failed with gRPC status: {ex.StatusCode}.");
                        if (retryCount >= MAX_RETRIES)
                        {
                            FileLogger.LogError($"Failed to connect to gRPC server for TurnOn after {MAX_RETRIES} attempts.");
                            break;                         }
                        int delayMs = 2000 * retryCount;
                        FileLogger.Log($"Waiting {delayMs}ms before next TurnOn retry...");
                        await Task.Delay(delayMs);
                    }
                    catch (Exception ex)                     {
                        FileLogger.LogError($"Error calling initial TurnOn service on attempt {retryCount}", ex);
                        break;                     }
                }
            }
            catch (Exception ex)
            {
                FileLogger.LogError("An unexpected error occurred during initial TurnOn preparation/logic", ex);
            }
            FileLogger.Log("Finished InitialTurnOnAsync.");
        }

        private async Task<bool> StartPythonServerFromBackendAsync()
        {
            FileLogger.Log("StartPythonServerFromBackendAsync started.");
            try
            {
                string baseDirectory = AppContext.BaseDirectory;
                string pythonBackendDir = Path.Combine(baseDirectory, "PythonBackend");
                string pythonExePath = Path.Combine(pythonBackendDir, "python.exe");
                string serverScriptPath = Path.Combine(pythonBackendDir, "serve.py");
                string libsInstallPath = Path.Combine(pythonBackendDir, "Lib", "site-packages");                 string requirementsPath = Path.Combine(pythonBackendDir, "requirements.txt");

                FileLogger.Log($"[CheckPaths] Base Directory: {baseDirectory}");
                FileLogger.Log($"[CheckPaths] Python Backend Dir: {pythonBackendDir}");
                FileLogger.Log($"[CheckPaths] Python Exe Path: {pythonExePath}");
                FileLogger.Log($"[CheckPaths] Server Script Path: {serverScriptPath}");
                FileLogger.Log($"[CheckPaths] Libs Install Path: {libsInstallPath}");
                FileLogger.Log($"[CheckPaths] Requirements Path: {requirementsPath}");


                                if (!Directory.Exists(pythonBackendDir))
                {
                    FileLogger.LogError($"[CheckFail] Python backend directory not found: {pythonBackendDir}");
                    return false;
                }
                FileLogger.Log("[CheckOK] Python backend directory exists.");

                if (!File.Exists(pythonExePath))
                {
                    FileLogger.LogError($"[CheckFail] Embedded Python executable not found: {pythonExePath}");
                    return false;
                }
                FileLogger.Log("[CheckOK] Python executable exists.");

                if (!File.Exists(serverScriptPath))
                {
                    FileLogger.LogError($"[CheckFail] Python server script not found: {serverScriptPath}");
                    return false;
                }
                FileLogger.Log("[CheckOK] Server script exists.");

                if (!File.Exists(requirementsPath))
                {
                    FileLogger.LogError($"[CheckFail] Requirements file not found: {requirementsPath}");
                                                        }
                else
                {
                    FileLogger.Log("[CheckOK] Requirements file exists.");
                }


                                FileLogger.Log("Checking Python dependencies...");
                bool depsInstalled = await CheckAndInstallPythonDependenciesAsync(pythonExePath, pythonBackendDir, libsInstallPath, requirementsPath);
                if (!depsInstalled)
                {
                                        FileLogger.LogError("Dependency installation failed. Cannot start Python server.");
                    return false;
                }
                FileLogger.Log("Python dependencies are ready.");

                                FileLogger.Log("Attempting to start Python server process...");
                return await StartPythonServerProcessAsync(pythonExePath, serverScriptPath, pythonBackendDir, libsInstallPath);
            }
            catch (Exception ex)
            {
                FileLogger.LogError("Fatal error during Python server startup sequence", ex);
                _pythonServerProcess = null;                 return false;
            }
        }

        private async Task<bool> StartPythonServerProcessAsync(string pythonExePath, string serverScriptPath,
                                                            string pythonBackendDir, string libsInstallPath)
        {
            string taskDescription = "PythonServer";             FileLogger.Log($"Starting Python server process: '{pythonExePath}' with script '{serverScriptPath}'");

            var startInfo = new ProcessStartInfo
            {
                FileName = pythonExePath,
                Arguments = $"\"{serverScriptPath}\"",                 WorkingDirectory = pythonBackendDir,
                UseShellExecute = false,
                CreateNoWindow = true,                 RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,                 StandardErrorEncoding = Encoding.UTF8
            };

                        startInfo.EnvironmentVariables["PYTHONPATH"] = libsInstallPath;             startInfo.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";             startInfo.EnvironmentVariables["PYTHONUNBUFFERED"] = "1";             FileLogger.Log($"Set Environment Variables: PYTHONPATH='{libsInstallPath}', PYTHONIOENCODING='utf-8', PYTHONUNBUFFERED='1'");


                        var serverProcess = new Process { StartInfo = startInfo, EnableRaisingEvents = true };

            var stdoutLines = new List<string>();             var stderrLines = new List<string>(); 
                        serverProcess.OutputDataReceived += (sender, args) =>
            {
                if (args.Data != null)
                {
                    FileLogger.LogProcessOutput(taskDescription, args.Data, isError: false);
                                        lock (stdoutLines) { stdoutLines.Add(args.Data); }
                }
            };

            serverProcess.ErrorDataReceived += (sender, args) =>
            {
                if (args.Data != null)
                {
                    FileLogger.LogProcessOutput(taskDescription, args.Data, isError: true);
                                        lock (stderrLines) { stderrLines.Add(args.Data); }
                }
            };

                        var processExitTcs = new TaskCompletionSource<bool>();
            serverProcess.Exited += (sender, e) => {
                FileLogger.Log($"Python server process (PID: {serverProcess?.Id}) has exited.");
                processExitTcs.TrySetResult(true);
            };


            try
            {
                FileLogger.Log($"Attempting to start process '{startInfo.FileName}'...");
                if (!serverProcess.Start())
                {
                    FileLogger.LogError("Failed to start Python server process (Process.Start returned false).");
                    return false;
                }
                FileLogger.Log($"Python server process started successfully (PID: {serverProcess.Id}). Beginning to read output.");

                                serverProcess.BeginOutputReadLine();
                serverProcess.BeginErrorReadLine();

                                _pythonServerProcess = serverProcess;

                                FileLogger.Log($"Waiting for server ready signal ('gRPC server started on port') in stdout (Timeout: {SERVER_START_TIMEOUT_MS}ms)...");
                var serverReadyTask = Task.Run(async () => {
                    int checkIntervalMs = 500;
                    int maxChecks = SERVER_START_TIMEOUT_MS / checkIntervalMs;
                    for (int i = 0; i < maxChecks; i++)
                    {
                                                if (serverProcess.HasExited || processExitTcs.Task.IsCompleted)
                        {
                            FileLogger.Log($"Server process exited prematurely during ready check (loop {i + 1}).");
                            return false;                         }

                                                lock (stdoutLines)
                        {
                            if (stdoutLines.Any(line => line != null && line.Contains("gRPC server started on port")))
                            {
                                FileLogger.Log($"Server ready signal found in stdout.");
                                return true;                             }
                        }
                        await Task.Delay(checkIntervalMs);                     }
                    FileLogger.Log($"Server ready signal not found within timeout.");
                    return false;                 });


                                Task completedTask = await Task.WhenAny(serverReadyTask, processExitTcs.Task, Task.Delay(SERVER_START_TIMEOUT_MS));

                bool isServerReady = false;
                if (completedTask == serverReadyTask)
                {
                    isServerReady = await serverReadyTask;                     FileLogger.Log($"Server ready check task completed. Is ready: {isServerReady}");
                }
                else if (completedTask == processExitTcs.Task)
                {
                    FileLogger.Log("Server process exited before ready check/timeout completed.");
                    isServerReady = false;
                }
                else                 {
                    FileLogger.Log("Server ready check timed out.");
                    isServerReady = false;
                }


                if (!isServerReady)
                {
                                        if (serverProcess.HasExited)
                    {
                        string errorDetails = "No recent stderr output captured.";
                        lock (stderrLines)
                        {
                            if (stderrLines.Count > 0) errorDetails = string.Join("\n", stderrLines.TakeLast(15));
                        }
                        FileLogger.LogError($"Python server process exited prematurely with code {serverProcess.ExitCode} before becoming ready.\nLast stderr lines:\n{errorDetails}");
                    }
                    else
                    {
                        FileLogger.LogError("Python server did not become ready within the expected time.");
                                                FileLogger.Log("Attempting to kill the non-responsive Python server process...");
                        try { if (!serverProcess.HasExited) serverProcess.Kill(true); }
                        catch (Exception killEx) { FileLogger.LogError("Exception while trying to kill hanging Python process", killEx); }
                    }

                    _pythonServerProcess = null;                     return false;                 }

                FileLogger.Log("Python server is ready and running.");
                return true;             }
            catch (Exception ex)
            {
                FileLogger.LogError("Exception occurred while starting or monitoring Python server process", ex);
                                try { if (serverProcess != null && !serverProcess.HasExited) serverProcess.Kill(true); }
                catch {  }
                _pythonServerProcess = null;                 return false;
            }
        }


        private async Task<bool> CheckAndInstallPythonDependenciesAsync(string pythonExe, string workingDir, string installTargetDir, string requirementsFilePath)
        {
            FileLogger.Log("CheckAndInstallPythonDependenciesAsync started.");
            string markerFile = Path.Combine(workingDir, ".dependencies_installed");

            if (File.Exists(markerFile))
            {
                FileLogger.Log($"Dependency marker file found: '{markerFile}'. Skipping installation.");
                return true;
            }

            FileLogger.Log("Python dependencies marker file not found. Installation required...");

                        await Dispatcher.InvokeAsync(() => {
                ShowLoading("Installing Python dependencies...");
                MessageBox.Show("Initializing Python dependencies. This may take several minutes on first launch.\nPlease wait...",
                               "Installing Dependencies", MessageBoxButton.OK, MessageBoxImage.Information);
            });

            if (!File.Exists(requirementsFilePath))
            {
                FileLogger.LogError($"requirements.txt not found at '{requirementsFilePath}'. Cannot install dependencies.");
                await Dispatcher.InvokeAsync(HideLoading);                 return false;
            }

            try
            {
                FileLogger.Log($"Ensuring target directory exists: '{installTargetDir}'");
                Directory.CreateDirectory(installTargetDir); 
                                FileLogger.Log("Checking if pip module exists in the embedded Python...");
                                bool pipExists = await RunPythonCommandAsync(pythonExe, "-m pip --version", workingDir, "Checking pip existence", 15000);

                if (!pipExists)
                {
                    FileLogger.Log("pip module not found. Attempting to bootstrap pip using get-pip.py...");
                    string getPipPath = Path.Combine(workingDir, "get-pip.py"); 
                                        try
                    {
                        FileLogger.Log("Downloading get-pip.py from bootstrap.pypa.io...");
                                                                        using (var httpClient = new HttpClient())
                        {
                                                        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("AlexAssistantApp/1.0");
                                                        byte[] scriptBytes = await httpClient.GetByteArrayAsync("https://bootstrap.pypa.io/get-pip.py");
                                                        await File.WriteAllBytesAsync(getPipPath, scriptBytes);
                            FileLogger.Log($"get-pip.py downloaded successfully to '{getPipPath}'.");
                        }
                    }
                    catch (HttpRequestException httpEx)
                    {
                        FileLogger.LogError($"HTTP error downloading get-pip.py: {httpEx.StatusCode}", httpEx);
                        ShowErrorToUser("Failed to download Python installer (get-pip.py). Please check your internet connection.");
                        await Dispatcher.InvokeAsync(HideLoading);
                        return false;
                    }
                    catch (Exception downloadEx)
                    {
                        FileLogger.LogError($"Failed to download or save get-pip.py", downloadEx);
                        ShowErrorToUser("Failed to download Python installer (get-pip.py).");
                        await Dispatcher.InvokeAsync(HideLoading);
                        return false;                     }

                                        FileLogger.Log($"Executing get-pip.py to install pip into '{installTargetDir}'...");
                                                            string bootstrapArgs = $"\"{getPipPath}\" --target=\"{installTargetDir}\" --no-warn-script-location";
                                        bool bootstrapSuccess = await RunPythonCommandAsync(pythonExe, bootstrapArgs, workingDir, "Bootstrapping pip", 600000); 
                                        try
                    {
                        FileLogger.Log($"Deleting temporary file '{getPipPath}'...");
                        File.Delete(getPipPath);
                    }
                    catch (Exception deleteEx)
                    {
                        FileLogger.Log($"Warning: could not delete temporary get-pip.py file: {deleteEx.Message}");
                    }

                                        if (!bootstrapSuccess)
                    {
                                                FileLogger.LogError("Failed to bootstrap pip using get-pip.py. Cannot proceed with dependency installation.");
                        ShowErrorToUser("Failed to install the Python package manager (pip).");
                        await Dispatcher.InvokeAsync(HideLoading);
                        return false;                     }
                    FileLogger.Log("pip bootstrapped and installed successfully.");
                }
                else
                {
                    FileLogger.Log("pip module already exists. Skipping bootstrap.");
                }
                

                                FileLogger.Log("Splitting requirements into essential and optional packages...");
                var essentialPackages = new List<string>();
                var optionalPackages = new List<string>();
                try
                {
                    var requirements = await File.ReadAllLinesAsync(requirementsFilePath);
                    foreach (var line in requirements)
                    {
                        if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#")) continue;
                                                if (line.StartsWith("torch") || line.StartsWith("torchvision") || line.StartsWith("torchaudio") || line.StartsWith("whisper") || line.StartsWith("openai-whisper"))
                            optionalPackages.Add(line);
                        else
                            essentialPackages.Add(line);
                    }
                    FileLogger.Log($"Split results: {essentialPackages.Count} essential, {optionalPackages.Count} optional.");
                }
                catch (Exception ex)
                {
                    FileLogger.LogError($"Failed to read or parse '{requirementsFilePath}'", ex);
                    ShowErrorToUser($"Error reading requirements file: {ex.Message}");
                    await Dispatcher.InvokeAsync(HideLoading);
                    return false;
                }
                

                                FileLogger.Log("Ensuring pip is up-to-date...");
                                string pipArgsUpdate = $"-m pip install --upgrade pip --target=\"{installTargetDir}\" --no-cache-dir --no-warn-script-location";
                bool pipUpdated = await RunPythonCommandAsync(pythonExe, pipArgsUpdate, workingDir, "Updating pip", 600000);                 if (!pipUpdated)
                {
                                        FileLogger.Log("Warning: Failed to upgrade pip. Continuing with the current version...");
                }
                else
                {
                    FileLogger.Log("pip is up-to-date.");
                }
                

                                if (essentialPackages.Count > 0)
                {
                    FileLogger.Log("Installing essential Python packages...");
                    string tempEssentialReqs = Path.Combine(workingDir, "essential_requirements_temp.txt");
                    try
                    {
                        await File.WriteAllLinesAsync(tempEssentialReqs, essentialPackages);
                        FileLogger.Log($"Created temporary requirements file: '{tempEssentialReqs}'");
                                                string essentialInstallArgs = $"-m pip install -r \"{tempEssentialReqs}\" --target=\"{installTargetDir}\" --no-cache-dir --no-warn-script-location";
                                                bool essentialInstalled = await RunPythonCommandAsync(pythonExe, essentialInstallArgs, workingDir,
                                                                             "Installing essential packages", DEPENDENCY_INSTALL_TIMEOUT_MS); 
                                                try { File.Delete(tempEssentialReqs); } catch { /* Ignore delete error */ }

                        if (!essentialInstalled)
                        {
                            FileLogger.LogError("Failed to install essential Python packages. Check logs for details from 'Installing essential packages' task.");
                            ShowErrorToUser("Failed to install essential Python packages.");
                            await Dispatcher.InvokeAsync(HideLoading);
                            return false;                         }
                        FileLogger.Log("Essential packages installed successfully.");
                    }
                    catch (Exception ex)
                    {
                        FileLogger.LogError("Error during essential package installation process", ex);
                        try { File.Delete(tempEssentialReqs); } catch { /* Ignore delete error */ }
                        ShowErrorToUser($"Error installing essential packages: {ex.Message}");
                        await Dispatcher.InvokeAsync(HideLoading);
                        return false;
                    }
                }
                else
                {
                    FileLogger.Log("No essential packages listed in requirements to install.");
                }
                

                                if (optionalPackages.Count > 0)
                {
                    FileLogger.Log("Installing optional (heavy) Python packages...");
                    string tempOptionalReqs = Path.Combine(workingDir, "optional_requirements_temp.txt");
                    try
                    {
                        await File.WriteAllLinesAsync(tempOptionalReqs, optionalPackages);
                        FileLogger.Log($"Created temporary optional requirements file: '{tempOptionalReqs}'");

                                                string torchExtraIndexUrl = "https://download.pytorch.org/whl/cu118";
                                                string optionalInstallArgs = $"-m pip install --extra-index-url {torchExtraIndexUrl} -r \"{tempOptionalReqs}\" " +
                                                    $"--target=\"{installTargetDir}\" --no-cache-dir --prefer-binary --no-warn-script-location";

                        bool success = false;
                        for (int attempt = 1; attempt <= MAX_INSTALL_RETRIES && !success; attempt++)
                        {
                            FileLogger.Log($"Optional packages install attempt {attempt}/{MAX_INSTALL_RETRIES}...");
                                                        success = await RunPythonCommandAsync(pythonExe, optionalInstallArgs, workingDir,
                                                             $"Installing optional packages (attempt {attempt})",
                                                             DEPENDENCY_INSTALL_TIMEOUT_MS); 
                            if (!success && attempt < MAX_INSTALL_RETRIES)
                            {
                                FileLogger.Log($"Optional package installation failed on attempt {attempt}. Retrying in 5 seconds...");
                                await Task.Delay(5000);
                            }
                        }

                                                try { File.Delete(tempOptionalReqs); } catch { /* Ignore */ }

                        if (!success)
                        {
                                                        FileLogger.LogError($"Failed to install optional Python packages after {MAX_INSTALL_RETRIES} attempts. Functionality might be limited.");
                            ShowErrorToUser("Warning: Failed to install some optional packages (like PyTorch/Whisper). Functionality might be limited.");
                        }
                        else
                        {
                            FileLogger.Log("Optional packages installed successfully.");
                        }
                    }
                    catch (Exception ex)
                    {
                        FileLogger.LogError("Error during optional package installation process", ex);
                        try { File.Delete(tempOptionalReqs); } catch { /* Ignore delete error */ }
                        ShowErrorToUser($"Error installing optional packages: {ex.Message}");
                                            }
                }
                else
                {
                    FileLogger.Log("No optional packages listed in requirements to install.");
                }
                

                                FileLogger.Log($"Attempting to create dependency marker file: '{markerFile}'");
                try
                {
                                        File.Create(markerFile).Close();
                    FileLogger.Log("Dependency marker file created successfully.");
                    return true;                 }
                catch (Exception ex)
                {
                                        FileLogger.LogError($"Failed to create marker file '{markerFile}'. Installation will run again on next launch.", ex);
                    return true;                 }
                            }
            catch (Exception ex)
            {
                                FileLogger.LogError("Unexpected error during the dependency installation sequence", ex);
                ShowErrorToUser($"Unexpected error installing dependencies: {ex.Message}");
                return false;
            }
            finally
            {
                                await Dispatcher.InvokeAsync(HideLoading);
                FileLogger.Log("CheckAndInstallPythonDependenciesAsync finished.");
            }
        }

        private async Task<bool> RunPythonCommandAsync(string pythonExe, string arguments, string workingDir,
                                                   string taskDescription, int timeoutMs = 600000)
        {
            FileLogger.Log($"\n--- Executing Python Task: {taskDescription} ---");
            FileLogger.Log($"Command: \"{pythonExe}\" {arguments}");
            FileLogger.Log($"WorkingDirectory: {workingDir}");

            var startInfo = new ProcessStartInfo
            {
                FileName = pythonExe,
                Arguments = arguments,
                WorkingDirectory = workingDir,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

                        startInfo.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";
            startInfo.EnvironmentVariables["PYTHONUNBUFFERED"] = "1";
            
                        using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };

            var processExitTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var lastErrorLines = new List<string>(15); 
            process.OutputDataReceived += (s, e) => {
                if (e.Data != null) FileLogger.LogProcessOutput(taskDescription, e.Data, isError: false);
            };
            process.ErrorDataReceived += (s, e) => {
                if (e.Data != null)
                {
                    FileLogger.LogProcessOutput(taskDescription, e.Data, isError: true);
                    lock (lastErrorLines)
                    {
                        if (lastErrorLines.Count >= 15) lastErrorLines.RemoveAt(0);
                        lastErrorLines.Add(e.Data);
                    }
                }
            };
            process.Exited += (s, e) => {
                FileLogger.Log($"Process for task '{taskDescription}' exited event fired.");
                processExitTcs.TrySetResult(true);
            };

            try
            {
                FileLogger.Log($"Starting process for task: {taskDescription}...");
                if (!process.Start())
                {
                    FileLogger.LogError($"Failed to start process for task: {taskDescription} (Start returned false)");
                    return false;
                }
                FileLogger.Log($"Process for '{taskDescription}' started (PID: {process.Id}). Reading output...");

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                FileLogger.Log($"Waiting for process '{taskDescription}' to exit (Timeout: {timeoutMs / 1000} sec)...");

                                Task completedTask = await Task.WhenAny(processExitTcs.Task, Task.Delay(timeoutMs));

                if (completedTask != processExitTcs.Task)                 {
                    FileLogger.LogError($"Python command '{taskDescription}' timed out after {timeoutMs / 1000} seconds. Terminating process...");
                    try { if (!process.HasExited) process.Kill(true); }                     catch (Exception killEx) { FileLogger.LogError($"Exception while killing timed out process '{taskDescription}'", killEx); }
                    return false;                 }

                                FileLogger.Log($"Process task '{taskDescription}' exited naturally or was terminated. Waiting briefly for output buffers...");
                                await Task.Delay(500); 
                int exitCode = -1;
                try { exitCode = process.ExitCode; } catch (Exception ex) { FileLogger.LogError($"Could not get ExitCode for '{taskDescription}'", ex); }

                FileLogger.Log($"--- Task '{taskDescription}' finished with ExitCode: {exitCode} ---");

                if (exitCode != 0)
                {
                    string errorSummary = "No recent error output captured.";
                    lock (lastErrorLines)
                    {
                        if (lastErrorLines.Count > 0) errorSummary = string.Join("\n", lastErrorLines);
                    }
                    FileLogger.LogError($"Task '{taskDescription}' failed with ExitCode: {exitCode}.\nRecent Error Output:\n{errorSummary}");
                    return false;                 }

                FileLogger.Log($"Task '{taskDescription}' completed successfully.");
                return true;             }
            catch (Exception ex)
            {
                FileLogger.LogError($"Exception during process execution for '{taskDescription}'", ex);
                                try { if (process != null && !process.HasExited) process.Kill(true); } catch {  }
                return false;
            }
        }


        private async Task StopPythonServerAsync(bool calledFromClosing = false)
        {
            FileLogger.Log($"StopPythonServerAsync called. Called from closing: {calledFromClosing}");
                        Process? processToStop = Interlocked.Exchange(ref _pythonServerProcess, null);

            if (processToStop == null)
            {
                FileLogger.Log("Python server process is already null or was stopped previously.");
                return;
            }

            int processId = -1;
            try { processId = processToStop.Id; } catch {}
            FileLogger.Log($"Attempting to stop Python server process (PID: {processId}).");

            try
            {
                if (!processToStop.HasExited)
                {
                    FileLogger.Log($"Process (PID: {processId}) is running. Attempting graceful shutdown (WaitForExit 2000ms)...");
                                        bool exitedGracefully = await Task.Run(() => processToStop.WaitForExit(2000));

                    if (exitedGracefully)
                    {
                        FileLogger.Log($"Process (PID: {processId}) exited gracefully.");
                    }
                    else if (!processToStop.HasExited)
                    {
                        FileLogger.Log($"Process (PID: {processId}) did not exit gracefully. Attempting to kill...");
                        try
                        {
                            processToStop.Kill(true);                             FileLogger.Log($"Process (PID: {processId}) killed.");
                                                        await Task.Run(() => processToStop.WaitForExit(1000));
                            FileLogger.Log($"Process (PID: {processId}) status after kill: HasExited={processToStop.HasExited}");
                        }
                        catch (Exception killEx)
                        {
                            FileLogger.LogError($"Exception while killing process (PID: {processId})", killEx);
                        }
                    }
                }
                else
                {
                    FileLogger.Log($"Process (PID: {processId}) had already exited before StopPythonServerAsync action.");
                }
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is System.ComponentModel.Win32Exception)
            {
                                FileLogger.LogError($"Error during Python process termination (process likely already gone or inaccessible): {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                                FileLogger.LogError("Unexpected error during Python process termination", ex);
            }
            finally
            {
                                                FileLogger.Log($"Disposing process object (PID: {processId})...");
                try
                {
                    processToStop.Dispose();
                }
                catch (Exception disposeEx)
                {
                    FileLogger.LogError($"Error disposing process object (PID: {processId})", disposeEx);
                }
                FileLogger.Log("Process object disposed.");

                                if (calledFromClosing)
                {
                    FileLogger.Log("Attempting to force release ports as called from closing...");
                    await ForceReleasePortsAsync();
                }
            }
            FileLogger.Log("StopPythonServerAsync finished.");
        }

                        private async Task ForceReleasePortsAsync()
        {
            FileLogger.Log("Attempting to force release gRPC ports (50051-50060)...");
            try
            {
                int basePort = 50051;
                for (int i = 0; i < 10; i++)
                {
                    int port = basePort + i;
                    FileLogger.Log($"Checking port {port} for listening process...");

                    var startInfo = new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = $"/c netstat -ano | findstr :{port} | findstr LISTENING",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    };

                    using var process = new Process { StartInfo = startInfo };
                    process.Start();
                    string output = await process.StandardOutput.ReadToEndAsync();
                    await process.WaitForExitAsync();

                    if (string.IsNullOrWhiteSpace(output))
                    {
                        FileLogger.Log($"Port {port} is not in LISTENING state.");
                        continue;
                    }

                    FileLogger.Log($"Output for port {port}:\n{output}");

                    foreach (string line in output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        string[] parts = line.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length > 4)
                        {
                            string pidStr = parts[parts.Length - 1];
                            if (int.TryParse(pidStr, out int pid) && pid > 0)
                            {
                                FileLogger.Log($"Found process with PID {pid} listening on port {port}. Attempting to kill...");
                                var killProcessInfo = new ProcessStartInfo
                                {
                                    FileName = "taskkill",
                                    Arguments = $"/F /PID {pid}",
                                    UseShellExecute = false,
                                    CreateNoWindow = true,
                                    RedirectStandardOutput = true,                                     RedirectStandardError = true
                                };
                                using var killer = new Process { StartInfo = killProcessInfo };
                                killer.Start();
                                string killOut = await killer.StandardOutput.ReadToEndAsync();
                                string killErr = await killer.StandardError.ReadToEndAsync();
                                await killer.WaitForExitAsync();
                                FileLogger.Log($"Taskkill for PID {pid} finished with ExitCode {killer.ExitCode}. Output: {killOut.Trim()}. Error: {killErr.Trim()}");
                            }
                        }
                    }
                }
                FileLogger.Log("Finished checking/killing processes holding ports.");
            }
            catch (Exception ex)
            {
                FileLogger.LogError("Error during forced port release process", ex);
            }
        }

        private async Task AttemptGrpcTurnOffAsync()
        {
            FileLogger.Log("Attempting to send gRPC TurnOff command...");
            try
            {
                                if (GrpcService.Instance?.Client == null)
                {
                    FileLogger.Log("Skipping TurnOff command: gRPC client is null (already disposed or never created).");
                    return;
                }

                var turnOffRequest = new TurnOffRequest();
                                var options = new CallOptions(deadline: DateTime.UtcNow.AddSeconds(5));

                FileLogger.Log("Sending TurnOff request...");
                var response = await GrpcService.Instance.Client.TurnOffAsync(turnOffRequest, options);
                FileLogger.Log($"Assistant TurnOff response: {response?.Message ?? "null"}");

                                FileLogger.Log("Waiting 1 second after sending TurnOff command...");
                await Task.Delay(1000);
            }
            catch (RpcException ex)
            {
                                FileLogger.LogError($"gRPC TurnOff call failed: Status={ex.StatusCode}, Detail='{ex.Status.Detail}'", ex);
                                if (ex.StatusCode == StatusCode.Unavailable || ex.StatusCode == StatusCode.Cancelled || ex.StatusCode == StatusCode.DeadlineExceeded)
                {
                    FileLogger.Log("TurnOff failed likely because server was already down or shutting down.");
                }
            }
            catch (ObjectDisposedException ex)
            {
                FileLogger.LogError("TurnOff command failed because gRPC client/channel was already disposed", ex);
            }
            catch (Exception ex)
            {
                                FileLogger.LogError("Unexpected error sending TurnOff command", ex);
            }
            FileLogger.Log("Finished AttemptGrpcTurnOffAsync.");
        }

        private async void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            FileLogger.Log($"MainWindow_Closing event. IsReallyClosing: {_isReallyClosing}, IsClosing flag: {_isClosing}");

            if (_isReallyClosing)             {
                if (!_isClosing)                 {
                    e.Cancel = true;                     _isClosing = true; 
                    FileLogger.Log("--- Full Application Shutdown Initiated ---");
                    Dispatcher.Invoke(() => ShowLoading("Closing application..."));

                    try
                    {
                                                FileLogger.Log("Cancelling server closing token source...");
                        _serverClosingTokenSource.Cancel();

                                                await AttemptGrpcTurnOffAsync();

                                                await Task.Delay(1500); 
                                                FileLogger.Log("Disposing gRPC channel...");
                        try
                        {
                            GrpcService.Instance?.DisposeChannel();                             FileLogger.Log("gRPC channel disposed.");
                        }
                        catch (Exception ex)
                        {
                            FileLogger.LogError("Error disposing gRPC channel", ex);
                        }

                                                await StopPythonServerAsync(calledFromClosing: true); 
                                                FileLogger.Log("Disposing server closing token source...");
                        _serverClosingTokenSource.Dispose();

                        FileLogger.Log("--- All cleanup operations completed. Proceeding with application shutdown. ---");
                    }
                    catch (Exception ex)
                    {
                        FileLogger.LogError("Error during application shutdown sequence", ex);
                                            }
                    finally
                    {
                                                FileLogger.Log("Invoking Application.Current.Shutdown().");
                                                System.Windows.Application.Current.Shutdown();
                    }
                }
                else
                {
                    FileLogger.Log("Shutdown already in progress, cancelling redundant close event.");
                    e.Cancel = true;                 }
            }
            else             {
                FileLogger.Log("Close button clicked (not really closing). Hiding to tray.");
                e.Cancel = true;                 HideToTray(); 
                                if (!Properties.Settings.Default.DontShowTrayNotification && _trayIcon != null)
                {
                    try
                    {
                        _trayIcon?.ShowBalloonTip(
                            3000,
                            "Alex Assistant",
                            "The program continues to run in the background.",
                            System.Windows.Forms.ToolTipIcon.Info
                        );
                    }
                    catch (Exception tipEx)
                    {
                        FileLogger.LogError("Failed to show balloon tip", tipEx);
                    }
                }
            }
        }


        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            FileLogger.Log("Close button UI element clicked.");
                                                                                                this.Close();         }


                                                        private void ShowErrorToUser(string message)
        {
                        if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => ShowErrorToUser(message));
                return;
            }
            FileLogger.Log($"Showing error message box to user: \"{message}\"");
            MessageBox.Show(message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }


                private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            FileLogger.Log("Minimize button clicked.");
            WindowState = WindowState.Minimized;
        }

        private void WindowDrag(object sender, MouseButtonEventArgs e)
        {
                        if (e.LeftButton == MouseButtonState.Pressed)
                DragMove();
        }

        public void NavigateWithFade(Page newPage)
        {
            string pageName = newPage?.GetType().Name ?? "null";
            FileLogger.Log($"Navigating with fade to page: {pageName}");
            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(200)) { EasingFunction = new QuadraticEase() };
            fadeOut.Completed += (s, e) =>
            {
                FileLogger.Log($"Fade out complete. Setting MainFrame content to {pageName}.");
                MainFrame.Content = newPage;
                var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200)) { EasingFunction = new QuadraticEase() };
                FileLogger.Log($"Starting fade in animation for {pageName}.");
                MainFrame.BeginAnimation(Frame.OpacityProperty, fadeIn);
            };
            FileLogger.Log($"Starting fade out animation from current page.");
            MainFrame.BeginAnimation(Frame.OpacityProperty, fadeOut);
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (MainFrame.CanGoBack)
            {
                FileLogger.Log("Back button clicked. Navigating back.");
                MainFrame.GoBack();
            }
            else
            {
                FileLogger.Log("Back button clicked, but cannot go back.");
            }
        }
    }
}