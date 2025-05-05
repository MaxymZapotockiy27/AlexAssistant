using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace AlexAssistant
{
    public partial class FullScreenOverlayWindow : Window
    {
        static readonly string VideoResourcePath = Properties.Settings.Default.OverlayPath;


        public FullScreenOverlayWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
           
            InitializeAndPlayVideo();
        }

        private void InitializeAndPlayVideo()
        {
            try
            {
            
                Uri videoUri = new Uri(VideoResourcePath);
                VideoPlayer.Source = videoUri;
                VideoPlayer.Play();
                Debug.WriteLine($"Attempting to play video resource: {videoUri}");
            }
            catch (Exception ex)
            {
          
                string errorMsg = $"Failed to load or play video '{VideoResourcePath}'.\nError: {ex.Message}";
                Debug.WriteLine($"ERROR: {errorMsg}");
                MessageBox.Show(errorMsg, "Video Playback Error", MessageBoxButton.OK, MessageBoxImage.Error);
                MainGrid.Background = Brushes.DarkSlateGray;
            }
        }
        private void VideoPlayer_MediaEnded(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("Video playback ended. Looping...");
            VideoPlayer.Position = TimeSpan.Zero;
            VideoPlayer.Play();
        }

        // Event handler called if an error occurs during media loading or playback.
        private void VideoPlayer_MediaFailed(object sender, ExceptionRoutedEventArgs e)
        {
            string errorMsg = $"Video playback failed for Source '{VideoPlayer.Source}'.\nError: {e.ErrorException.Message}";
            if (e.ErrorException.InnerException != null)
            {
                errorMsg += $"\nInner Exception: {e.ErrorException.InnerException.Message}";
            }
            Debug.WriteLine($"ERROR: {errorMsg}");
            MessageBox.Show($"An error occurred while trying to play the background video.\nDetails: {e.ErrorException.Message}",
                            "Video Playback Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            VideoPlayer.Stop();
            VideoPlayer.Source = null;
            MainGrid.Background = Brushes.Navy;
        }

        // Event handler for key presses on the window.
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                this.Close();
            }
        }

        // Event handler called just before the window is closed and unloaded.
        private void Window_Unloaded(object sender, RoutedEventArgs e)
        {
            VideoPlayer.Stop();
            VideoPlayer.Source = null;
            Debug.WriteLine("Window unloaded. Video player stopped and source cleared.");
        }
    }
}