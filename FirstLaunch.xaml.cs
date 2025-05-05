using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading; using System.Windows.Media.Animation; 
namespace AlexAssistant
{
    public partial class FirstLaunch : Page
    {
                private List<BitmapSource> frames = new List<BitmapSource>();
        private int currentFrame = 0;
        private bool isAnimationRunning = false;
        private const double FramesPerSecond = 24;
        private TimeSpan frameInterval = TimeSpan.FromSeconds(1.0 / FramesPerSecond);
        private DateTime lastFrameTime;
        private const string AnimationResourceFolderPath = "/Animations/AnimationAssitant";
        private const int NumberOfFrames = 48;
        private MainWindow _mainwindow;

                private DispatcherTimer _textChangeTimer;
        private List<string> _textsToDisplay = new List<string>
        {
            "Hello, I am Alex.",
            "Привіт, я — Алекс",
            "Hola, soy Alex",
            "Bonjour, je suis Alex",
            "你好，我是Alex"
        };
        private int _currentTextIndex = 0;
        private TimeSpan _textFadeDuration = TimeSpan.FromSeconds(0.5);         private TimeSpan _textDisplayInterval = TimeSpan.FromSeconds(5.0); 
        public FirstLaunch(MainWindow mainwindow)
        {
            InitializeComponent();
            LoadFramesFromResources();             InitializeTextAnimation();               _mainwindow = mainwindow;
        }

        private void InitializeTextAnimation()
        {
                        if (_textsToDisplay.Count > 0)
            {
                ChangingTextBlock.Text = _textsToDisplay[_currentTextIndex];
            }
            else
            {
                ChangingTextBlock.Visibility = Visibility.Collapsed;             }


            _textChangeTimer = new DispatcherTimer();
                                                            _textChangeTimer.Interval = _textDisplayInterval;
            _textChangeTimer.Tick += TextChangeTimer_Tick;
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            StartAnimation();             if (_textsToDisplay.Count > 0)
            {
                _textChangeTimer.Start();             }
            Debug.WriteLine("FirstLaunch Page Loaded - Animations Started");
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            StopAnimation();             _textChangeTimer?.Stop();             Debug.WriteLine("FirstLaunch Page Unloaded - Animations Stopped");
        }

        private void TextChangeTimer_Tick(object? sender, EventArgs e)
        {
            AnimateTextChange();
        }

        private void AnimateTextChange()
        {
            if (ChangingTextBlock == null || _textsToDisplay.Count == 0) return;

                        var fadeOutAnimation = new DoubleAnimation
            {
                To = 0.0,                                     Duration = new Duration(_textFadeDuration),
                FillBehavior = FillBehavior.Stop             };

                        var storyboardFadeOut = new Storyboard();
            storyboardFadeOut.Children.Add(fadeOutAnimation);
            Storyboard.SetTarget(fadeOutAnimation, ChangingTextBlock);
            Storyboard.SetTargetProperty(fadeOutAnimation, new PropertyPath(TextBlock.OpacityProperty));

                        fadeOutAnimation.Completed += (s, eArgs) =>
            {
                                _currentTextIndex = (_currentTextIndex + 1) % _textsToDisplay.Count;
                ChangingTextBlock.Text = _textsToDisplay[_currentTextIndex];

                                var fadeInAnimation = new DoubleAnimation
                {
                    From = 0.0,                     To = 1.0,                       Duration = new Duration(_textFadeDuration)
                                    };

                                var storyboardFadeIn = new Storyboard();
                storyboardFadeIn.Children.Add(fadeInAnimation);
                Storyboard.SetTarget(fadeInAnimation, ChangingTextBlock);
                Storyboard.SetTargetProperty(fadeInAnimation, new PropertyPath(TextBlock.OpacityProperty));

                                storyboardFadeIn.Begin(ChangingTextBlock);             };

                        storyboardFadeOut.Begin(ChangingTextBlock);         }


                private void LoadFramesFromResources()
        {
            frames.Clear();
            List<BitmapSource> loadedFrames = new List<BitmapSource>();
            bool errorOccurred = false;

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

                    if (image.CanFreeze)
                    {
                        image.Freeze();
                    }
                    loadedFrames.Add(image);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error loading frame resource '{uriString}': {ex.Message}");
                    errorOccurred = true;
                }
            }

            if (errorOccurred || loadedFrames.Count < NumberOfFrames)
            {
                Debug.WriteLine($"Warning: Loaded only {loadedFrames.Count} out of {NumberOfFrames} expected frames from resources.");
            }

            if (loadedFrames.Count > 0)
            {
                frames = loadedFrames;
                AnimationImage.Source = frames[0];
                Debug.WriteLine($"Successfully loaded {frames.Count} frames from resources.");
            }
            else
            {
                Debug.WriteLine("Error: No frames were loaded from resources.");
                //MessageBox.Show("", "", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void StartAnimation()
        {
            if (isAnimationRunning || frames.Count == 0) return;

            CompositionTarget.Rendering -= CompositionTarget_Rendering;
            CompositionTarget.Rendering += CompositionTarget_Rendering;
            lastFrameTime = DateTime.Now;
            isAnimationRunning = true;
                    }

        private void StopAnimation()
        {
            if (!isAnimationRunning) return;
            CompositionTarget.Rendering -= CompositionTarget_Rendering;
            isAnimationRunning = false;
        }

        private void CompositionTarget_Rendering(object? sender, EventArgs e)
        {
            if (!isAnimationRunning || frames.Count == 0) return;

            var renderingTime = (e as RenderingEventArgs)?.RenderingTime;
                        if (DateTime.Now - lastFrameTime < frameInterval) return;

            currentFrame = (currentFrame + 1) % frames.Count;
            AnimationImage.Source = frames[currentFrame];
            lastFrameTime = DateTime.Now;
        }

        private void Page_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            NavigationService.Navigate(new SecondPage(_mainwindow));
        }
    }
}