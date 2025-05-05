using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Configuration;
using System.IO;
using OpenCvSharp;
using AlexAssistant.Models;
using System.Text.Json;
using AlexAssistant.Services;
using Control;
using Grpc.Core;


namespace AlexAssistant
{
    public partial class DashboardPage : Page
    {
        private MainWindow _mainWindow;
        private string userFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AlexAssistant",
            "userData.json");
        public DashboardPage(MainWindow mainWindow)
        {
            InitializeComponent();
            _mainWindow = mainWindow;
            LoadUserInfo();

        }

        private void LoadUserInfo()
        {
            string json = File.ReadAllText(userFilePath);
            UserData? user = JsonSerializer.Deserialize<UserData>(json);


            string userName = user.UserName;
            if (!string.IsNullOrEmpty(userName))
            {
                WelcomeMessage.Text = $"Welcome back, {userName}!";
            }
            else
            {
                WelcomeMessage.Text = "Welcome to Alex Assistant!";
            }
            bool assistantEnabled = Properties.Settings.Default.AssistantEnabled;
            AssistantToggle.IsChecked = assistantEnabled;
            UpdateStatusIndicators();
        }

        private void UpdateStatusIndicators()
        {
            if (AssistantToggle.IsChecked == true)
            {
                VoiceStatus.Text = "Active";
                VoiceStatus.Foreground = System.Windows.Media.Brushes.LimeGreen;
            }
            else
            {
                VoiceStatus.Text = "Inactive";
                VoiceStatus.Foreground = System.Windows.Media.Brushes.Gray;
            }
        }

        private async void AssistantToggle_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not ToggleButton toggleButton) return;

            bool isChecked = toggleButton.IsChecked ?? false;

                        Properties.Settings.Default.AssistantEnabled = isChecked;
            Properties.Settings.Default.Save();

                        UpdateStatusIndicators();

                        try
            {
                if (isChecked)                 {
                    Console.WriteLine("Attempting to turn ON assistant via gRPC...");
                                        string microphoneName = Properties.Settings.Default.MicrophoneName;
                    if (string.IsNullOrEmpty(microphoneName))
                    {
                                                MessageBox.Show("Microphone name is not configured in settings.", "Configuration Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                                                toggleButton.IsChecked = false;
                        Properties.Settings.Default.AssistantEnabled = false;
                        Properties.Settings.Default.Save();
                        UpdateStatusIndicators();
                        return;
                    }

                    var turnOnRequest = new TurnOnRequest { MicNameTarget = microphoneName };
                    var response = await GrpcService.Instance.Client.TurnOnAsync(turnOnRequest);
                    Console.WriteLine($"Assistant TurnOn response: {response.Message}");
                                    }
                else                 {
                    Console.WriteLine("Attempting to turn OFF assistant via gRPC...");
                    var turnOffRequest = new TurnOffRequest();                     var response = await GrpcService.Instance.Client.TurnOffAsync(turnOffRequest);
                    Console.WriteLine($"Assistant TurnOff response: {response.Message}");
                                    }
            }
            catch (RpcException ex)
            {
                Console.WriteLine($"Error calling gRPC service: {ex.Status.StatusCode} - {ex.Status.Detail}");
                MessageBox.Show($"Failed to communicate with the assistant service: {ex.Status.Detail}", "gRPC Error", MessageBoxButton.OK, MessageBoxImage.Error);
                                toggleButton.IsChecked = !isChecked;                 Properties.Settings.Default.AssistantEnabled = !isChecked;
                Properties.Settings.Default.Save();
                UpdateStatusIndicators();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An unexpected error occurred during gRPC call: {ex.Message}");
                MessageBox.Show($"An unexpected error occurred: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                                toggleButton.IsChecked = !isChecked;
                Properties.Settings.Default.AssistantEnabled = !isChecked;
                Properties.Settings.Default.Save();
                UpdateStatusIndicators();
            }
        }

                private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            _mainWindow.NavigateWithFade(new Settings(_mainWindow));
        }
        private void RemindersButton_Click(object sender, RoutedEventArgs e)
        {
            _mainWindow.NavigateWithFade(new Reminder(_mainWindow));
        }

        private void ConfidenceButtonButton_Click(object sender, RoutedEventArgs e)
        {

            _mainWindow.NavigateWithFade(new OverlaySetPage(_mainWindow));
        }

        private void AddComands_Click(object sender, RoutedEventArgs e)
        {
            _mainWindow.NavigateWithFade(new CommandInputPage(_mainWindow));
        }
    }
}