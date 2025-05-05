using AlexAssistant.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel; 
using System.IO;                   
using System.Text.Json;             
using System.Windows;
using System.Windows.Controls;

namespace AlexAssistant
{
    public enum CommandActionType
    {
        Link,
        Path
    }
    public partial class CommandInputPage : Page
    {
        public MainWindow _mainwindow;
        public ObservableCollection<CommandItem> SavedCommands { get; set; }
        private readonly string _saveFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AlexAssistant", 
            "commands.json");
        public CommandInputPage(MainWindow mainWindow)
        {
            _mainwindow = mainWindow;
            InitializeComponent();
            SavedCommands = new ObservableCollection<CommandItem>();
            LoadCommands();
            CommandsListView.ItemsSource = SavedCommands; 
            DataContext = this;
            EnsureSaveDirectoryExists();
            UpdatePlaceholderVisibility(PhraseInput, PhrasePlaceholder);
            UpdatePlaceholderVisibility(ActionTargetInput, ActionTargetPlaceholder);
            UpdateActionTargetPlaceholderText();
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
                Console.WriteLine($"Error creating save directory: {ex.Message}");
                MessageBox.Show($"Could not create directory for saving commands: {ex.Message}", "Save Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        private void Input_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                TextBlock? placeholder = null;
                if (textBox.Name == "PhraseInput")
                {
                    placeholder = PhrasePlaceholder;
                }
                else if (textBox.Name == "ActionTargetInput")
                {
                    placeholder = ActionTargetPlaceholder;
                }

                UpdatePlaceholderVisibility(textBox, placeholder);
            }
        }

        private void UpdatePlaceholderVisibility(TextBox textBox, TextBlock? placeholder)
        {
            if (placeholder != null)
            {
                placeholder.Visibility = string.IsNullOrEmpty(textBox.Text) ? Visibility.Visible : Visibility.Collapsed;
            }
        }


        private void ActionType_Changed(object sender, RoutedEventArgs e)
        {
            UpdateActionTargetPlaceholderText();
        }

        private void UpdateActionTargetPlaceholderText()
        {
            if (ActionTargetPlaceholder == null) return; 

            if (LinkRadioButton.IsChecked == true)
            {
                ActionTargetPlaceholder.Text = "Enter the URL to open...";
            }
            else if (PathRadioButton.IsChecked == true)
            {
                ActionTargetPlaceholder.Text = "Enter the file or folder path...";
            }
          
            UpdatePlaceholderVisibility(ActionTargetInput, ActionTargetPlaceholder);
        }


   
        private void AddCommand_Click(object sender, RoutedEventArgs e)
        {
            string phrase = PhraseInput.Text.Trim();
            string actionTarget = ActionTargetInput.Text.Trim();
            CommandActionType actionType = LinkRadioButton.IsChecked == true ? CommandActionType.Link : CommandActionType.Path;

       
            if (string.IsNullOrWhiteSpace(phrase))
            {
                MessageBox.Show("Please enter a command phrase.", "Input Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                PhraseInput.Focus();
                return;
            }
            if (string.IsNullOrWhiteSpace(actionTarget))
            {
                string targetTypeName = actionType == CommandActionType.Link ? "URL" : "path";
                MessageBox.Show($"Please enter the {targetTypeName} for the action.", "Input Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                ActionTargetInput.Focus();
                return;
            }

      
            if (SavedCommands.Any(cmd => cmd.Phrase.Equals(phrase, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show($"The phrase '{phrase}' is already used for another command.", "Duplicate Phrase", MessageBoxButton.OK, MessageBoxImage.Warning);
                PhraseInput.Focus();
                return;
            }


    
            var newCommand = new CommandItem
            {
                Phrase = phrase,
                ActionType = actionType,
                ActionTarget = actionTarget
            };

            SavedCommands.Add(newCommand);

    
            SaveCommands();

        
            PhraseInput.Clear();
            ActionTargetInput.Clear();

    
            UpdatePlaceholderVisibility(PhraseInput, PhrasePlaceholder);
            UpdatePlaceholderVisibility(ActionTargetInput, ActionTargetPlaceholder);

      
            PhraseInput.Focus();
        }

 
        private void RemoveCommand_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button removeButton && removeButton.CommandParameter is CommandItem commandToRemove)
            {
          
                MessageBoxResult result = MessageBox.Show(
                    $"Are you sure you want to remove the command for the phrase:\n'{commandToRemove.Phrase}'?",
                    "Confirm Deletion",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    SavedCommands.Remove(commandToRemove);
                    SaveCommands(); 
                }
            }
        }

  
        private void SaveCommands()
        {
            try
            {
                EnsureSaveDirectoryExists(); 
                string json = JsonSerializer.Serialize(SavedCommands, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_saveFilePath, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving commands: {ex.Message}", "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadCommands()
        {
            try
            {
                EnsureSaveDirectoryExists(); 
                if (File.Exists(_saveFilePath))
                {
                    string json = File.ReadAllText(_saveFilePath);
                    var loadedCommands = JsonSerializer.Deserialize<List<CommandItem>>(json); 

                    if (loadedCommands != null)
                    {
                        SavedCommands.Clear(); 
                        foreach (var cmd in loadedCommands)
                        {
                            SavedCommands.Add(cmd); 
                        }
                    }
                }
            }
          
            catch (FileNotFoundException) {  }
            catch (JsonException ex)
            {
                MessageBox.Show($"Error loading commands: The format of the saved file is invalid.\nDetails: {ex.Message}", "Load Error", MessageBoxButton.OK, MessageBoxImage.Warning);
               
            }
            catch (Exception ex) 
            {
                MessageBox.Show($"Error loading commands: {ex.Message}", "Load Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}