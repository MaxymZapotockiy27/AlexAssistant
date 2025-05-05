using AlexAssistant.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Navigation; // Added for NavigationService

namespace AlexAssistant
{
    /// <summary>
    /// Interaction logic for Reminder.xaml
    /// </summary>
    public partial class Reminder : Page
    {
        // ObservableCollection automatically notifies the UI when items are added/removed
        public ObservableCollection<RemindersModel> AllSavedReminders { get; set; }
        // This collection will be bound to the ItemsControl and will hold the filtered items
        public ObservableCollection<RemindersModel> DisplayedReminders { get; set; }
        public MainWindow MainWindow;
        private readonly string _saveFilePath = Path.Combine(
           Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
           "AlexAssistant",
           "reminders.json");

        public Reminder(MainWindow mainWindow)
        
        
        {
            InitializeComponent();
            AllSavedReminders = new ObservableCollection<RemindersModel>();
            DisplayedReminders = new ObservableCollection<RemindersModel>();
            EnsureSaveDirectoryExists();
            LoadReminders(); // Load all reminders first
            InitializeTimePickers();
            RemindersItemsControl.ItemsSource = DisplayedReminders; // Bind the ItemsControl to the displayed list
            FilterReminders(); // Apply initial filter (usually "All")
            MainWindow = mainWindow;
        }

        private void InitializeTimePickers()
        {
            // Populate hours (00-23)
            for (int i = 0; i < 24; i++)
            {
                HourPicker.Items.Add(i.ToString("00"));
            }

            // Populate minutes (00, 05, 10, ..., 55)
            for (int i = 0; i < 60; i += 5) // Changed increment to 5 as is common
            {
                MinutePicker.Items.Add(i.ToString("00"));
            }

            SetDefaultTime();
        }

        private void SetDefaultTime()
        {
            // Set default time in pickers (current time rounded up to next 5 minutes)
            DateTime now = DateTime.Now;
            int currentHour = now.Hour;
            int currentMinute = now.Minute;

            int nextMinuteInterval = ((currentMinute + 4) / 5) * 5; // Round up to nearest 5

            if (nextMinuteInterval >= 60)
            {
                nextMinuteInterval = 0;
                currentHour = (currentHour + 1) % 24; // Increment hour, wrap around if needed
            }

            HourPicker.SelectedItem = currentHour.ToString("00");
            MinutePicker.SelectedItem = nextMinuteInterval.ToString("00");

            // Set the date picker to today's date
            ReminderDatePicker.SelectedDate = DateTime.Today;
        }


        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            // Use NavigationService if available (typical in Frame-based navigation)
            if (NavigationService != null && NavigationService.CanGoBack)
            {
                NavigationService.GoBack();
            }
            else
            {
                // Fallback or alternative action if not in a NavigationService context
                // For example, close the window if this is the main content
                // Or raise an event for the parent window to handle
                // MessageBox.Show("Cannot go back."); // Or log
            }
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            // Clear fields and set defaults before showing the popup
            NewReminderTitleTextBox.Text = "";
            NewReminderTaskTextBox.Text = "";
            ImportantCheckBox.IsChecked = false;
            SetDefaultTime(); // Reset time pickers to default

            AddReminderPopupOverlay.Visibility = Visibility.Visible;
            NewReminderTitleTextBox.Focus(); // Set focus to the title field
        }

        private void EnsureSaveDirectoryExists()
        {
            try
            {
                string? directory = Path.GetDirectoryName(_saveFilePath);
                if (directory != null && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
            }
            catch (Exception ex)
            {
               
                MessageBox.Show($"Unable to create directory to store reminders: {ex.Message}",
                                "Access error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void LoadReminders()
        {
            EnsureSaveDirectoryExists(); 
            if (!File.Exists(_saveFilePath))
            {
                AllSavedReminders.Clear(); 
                return; 
            }

            try
            {
                string json = File.ReadAllText(_saveFilePath);
               
                if (string.IsNullOrWhiteSpace(json))
                {
                    AllSavedReminders.Clear();
                    return;
                }

                var loadedReminders = JsonSerializer.Deserialize<List<RemindersModel>>(json);

                AllSavedReminders.Clear(); // Clear existing items before loading
                if (loadedReminders != null)
                {
                    foreach (var reminder in loadedReminders.OrderBy(r => TimeSpan.Parse(r.TimeTex))) // Optional: Load sorted by time
                    {
                        AllSavedReminders.Add(reminder);
                    }
                }
            }
            catch (JsonException ex)
            {
                MessageBox.Show($"Error reading reminder file(invalid format): { ex.Message}",
                               "Download error", MessageBoxButton.OK, MessageBoxImage.Error);
                AllSavedReminders.Clear(); // Clear potentially corrupt data
            }
            catch (Exception ex) // Catch other potential exceptions (IO, Security, etc.)
            {
                MessageBox.Show($"Failed to load reminder: {ex.Message}",
                                "Download error", MessageBoxButton.OK, MessageBoxImage.Error);
                AllSavedReminders.Clear();
            }
        }

        private void SaveReminders()
        {
            EnsureSaveDirectoryExists(); // Ensure directory exists before saving
            try
            {
                // Sort reminders before saving (optional, but keeps file consistent)
                var remindersToSave = AllSavedReminders.OrderBy(r => r.IsCompleted)
                                                      .ThenByDescending(r => r.IsImportant)
                                                      .ThenBy(r => TimeSpan.Parse(r.TimeTex))
                                                      .ToList();

                string json = JsonSerializer.Serialize(remindersToSave, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_saveFilePath, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving reminders:{ex.Message}",
                                "Saving error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveReminderButton_Click(object sender, RoutedEventArgs e)
        {
            string title = NewReminderTitleTextBox.Text.Trim();
            string task = NewReminderTaskTextBox.Text.Trim();

            // Get selected time
            string hour = HourPicker.SelectedItem?.ToString() ?? "00";
            string minute = MinutePicker.SelectedItem?.ToString() ?? "00";
            string time = $"{hour}:{minute}"; // Format as HH:mm

            // Get selected date (default to today if none selected)
            DateTime scheduledDate = ReminderDatePicker.SelectedDate ?? DateTime.Today;

            bool isImportant = ImportantCheckBox.IsChecked ?? false;

            if (string.IsNullOrWhiteSpace(title))
            {
                MessageBox.Show("Please enter a reminder title.",
                                "Title required", MessageBoxButton.OK, MessageBoxImage.Warning);
                NewReminderTitleTextBox.Focus();
                return;
            }

            // Basic time validation
            if (!TimeSpan.TryParse(time, out _))
            {
                MessageBox.Show("Please select the correct time",
                                "Wrong time", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var newReminder = new RemindersModel()
            {
                TitleTex = title,
                TaskTex = task,
                TimeTex = time,
                IsImportant = isImportant,
                IsCompleted = false,
                CreatedDate = DateTime.Now,
                ScheduledDate = scheduledDate  
            };

            AllSavedReminders.Add(newReminder);
            SaveReminders();
            FilterReminders();
            App.ReminderService.AddReminder(newReminder);

            AddReminderPopupOverlay.Visibility = Visibility.Collapsed;
        }

        private void CancelReminderButton_Click(object sender, RoutedEventArgs e)
        {
            AddReminderPopupOverlay.Visibility = Visibility.Collapsed;
        }

        private void AddReminderPopupOverlay_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // Close the popup only if the click is directly on the overlay (the Grid background)
            // and not on the Border containing the controls.
            if (e.Source == AddReminderPopupOverlay)
            {
                AddReminderPopupOverlay.Visibility = Visibility.Collapsed;
            }
        }

        // Handles CheckBox click inside the ItemsControl template
        private void Reminder_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox checkBox && checkBox.Tag is string reminderId)
            {
                var reminder = AllSavedReminders.FirstOrDefault(r => r.Id == reminderId);
                if (reminder != null)
                {
                    // Update the IsCompleted status based on the checkbox state
                    reminder.IsCompleted = checkBox.IsChecked ?? false;
                    SaveReminders();    // Save the change
                    FilterReminders();  // Re-apply filter to potentially hide/show the item
                }
            }
        }

        // Handles Delete button click inside the ItemsControl template
        private void DeleteReminder_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string reminderId)
            {
                var reminderToDelete = AllSavedReminders.FirstOrDefault(r => r.Id == reminderId);
                if (reminderToDelete != null)
                {
                    MessageBoxResult result = MessageBox.Show(
                        $"Do you really want to delete the reminder? '{reminderToDelete.TitleTex}'?",
                        "Confirm deletion",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        AllSavedReminders.Remove(reminderToDelete); // Remove from master list
                        SaveReminders();           // Save the changes
                        FilterReminders();         // Update the displayed list
                    }
                }
            }
        }

        // Event handler for all filter RadioButtons
        private void FilterRadio_Checked(object sender, RoutedEventArgs e)
        {
            // Only filter if the controls have been initialized
            if (IsLoaded)
            {
                FilterReminders();
            }
        }

        // Applies the current filter to the AllSavedReminders and updates DisplayedReminders
        private void FilterReminders()
        {
            IEnumerable<RemindersModel> filteredQuery;

            if (AllRadio.IsChecked == true)
            {
                filteredQuery = AllSavedReminders.Where(r => !r.IsCompleted);
            }
            else if (TodayRadio.IsChecked == true)
            {
                // Now using the dedicated ScheduledDate property
                DateTime today = DateTime.Today;
                filteredQuery = AllSavedReminders.Where(r => !r.IsCompleted && r.ScheduledDate.Date == today);
            }
            else if (ImportantRadio.IsChecked == true)
            {
                filteredQuery = AllSavedReminders.Where(r => !r.IsCompleted && r.IsImportant);
            }
            else if (CompleteRadio.IsChecked == true)
            {
                filteredQuery = AllSavedReminders.Where(r => r.IsCompleted);
            }
            else
            {
                filteredQuery = AllSavedReminders.Where(r => !r.IsCompleted);
            }

            // Update the sorting to include date in the order
            var sortedFilteredList = filteredQuery
                .OrderBy(r => r.ScheduledDate.Date)  // First by date
                .ThenBy(r => TimeSpan.Parse(r.TimeTex))  // Then by time
                .ToList();

            DisplayedReminders.Clear();
            foreach (var item in sortedFilteredList)
            {
                DisplayedReminders.Add(item);
            }
        }
    }
}