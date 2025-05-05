using AlexAssistant.Data;
using AlexAssistant.Models;
using AlexAssistant.Services;
using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace AlexAssistant
{
    public partial class SecondPage : Page
    {
        private CityService _cityService;
        private List<WorldCity> _foundCities;
        private MainWindow _mainWindow;
        public SecondPage(MainWindow mainWindow)
        {
            InitializeComponent();
            _cityService = new CityService();
            _foundCities = new List<WorldCity>();
            _mainWindow = mainWindow;
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            SetupDateFieldsNavigation();

                        NameCity.LostFocus += (s, evt) => {
                System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(
                    new Action(() => {
                        if (!CitySuggestions.IsKeyboardFocusWithin)
                        {
                            CitySuggestionsPopup.IsOpen = false;
                        }
                    }),
                    System.Windows.Threading.DispatcherPriority.Background);
            };
        }
        private void NavigateNext_Click(object sender, RoutedEventArgs e)
        {
                        if (string.IsNullOrWhiteSpace(NameInput.Text) || NameInput.Text == NameInput.Tag?.ToString())
            {
                MessageBox.Show("Please enter your name.", "Validation error", MessageBoxButton.OK, MessageBoxImage.Warning);
                NameInput.Focus();
                return;
            }

                        if (!_isCitySelected || string.IsNullOrWhiteSpace(_selectedCity))
            {
                MessageBox.Show("Please select a city from the list.", "Validation error", MessageBoxButton.OK, MessageBoxImage.Warning);
                NameCity.Focus();
                return;
            }

                        DateTime? birthday = GetBirthdayFromInputs();
            if (!birthday.HasValue)
            {
                MessageBox.Show("Please enter your correct date of birth.", "Validation error", MessageBoxButton.OK, MessageBoxImage.Warning);
                DayInput.Focus();
                return;
            }

                        var userDataManager = new UserService();
            var userData = new UserData
            {
                UserName = NameInput.Text,
                UserCity = _selectedCity,
                UserCountry = _selectedCountry,
                UserBirthday = birthday
            };

            userDataManager.SaveUserData(userData);
            NavigationService.Navigate(new DashboardPage(_mainWindow));
        }
        private void TextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            TextBox textBox = (TextBox)sender;
                        if (textBox.Text == textBox.Tag?.ToString())
            {
                textBox.Text = string.Empty;
            }
            textBox.SelectAll();
        }

        private void TextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            TextBox textBox = (TextBox)sender;
                        if (string.IsNullOrWhiteSpace(textBox.Text) && textBox.Tag != null)
            {
                textBox.Text = textBox.Tag.ToString();
            }

                        if (textBox == DayInput && textBox.Text != textBox.Tag?.ToString())
            {
                int day;
                if (int.TryParse(textBox.Text, out day))
                {
                    if (day < 1) day = 1;
                    if (day > 31) day = 31;
                    textBox.Text = day.ToString("00");
                }
            }
            else if (textBox == MonthInput && textBox.Text != textBox.Tag?.ToString())
            {
                int month;
                if (int.TryParse(textBox.Text, out month))
                {
                    if (month < 1) month = 1;
                    if (month > 12) month = 12;
                    textBox.Text = month.ToString("00");
                }
            }
            else if (textBox == YearInput && textBox.Text != textBox.Tag?.ToString())
            {
                int year;
                if (int.TryParse(textBox.Text, out year))
                {
                    if (year < 1900) year = 1900;
                    if (year > DateTime.Now.Year) year = DateTime.Now.Year;
                    textBox.Text = year.ToString();
                }
            }
        }

                private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            Regex regex = new Regex("[^0-9]+");
            e.Handled = regex.IsMatch(e.Text);
        }

      
        private void DateInput_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            TextBox currentTextBox = sender as TextBox;

            if (e.Key == Key.Left)
            {
                                if (currentTextBox.CaretIndex == 0)
                {
                                        if (currentTextBox == MonthInput)
                    {
                        DayInput.Focus();
                        DayInput.CaretIndex = DayInput.Text.Length;
                        e.Handled = true;
                    }
                    else if (currentTextBox == YearInput)
                    {
                        MonthInput.Focus();
                        MonthInput.CaretIndex = MonthInput.Text.Length;
                        e.Handled = true;
                    }
                }
            }
            else if (e.Key == Key.Right)
            {
                                if (currentTextBox.CaretIndex == currentTextBox.Text.Length)
                {
                                        if (currentTextBox == DayInput)
                    {
                        MonthInput.Focus();
                        MonthInput.CaretIndex = 0;
                        e.Handled = true;
                    }
                    else if (currentTextBox == MonthInput)
                    {
                        YearInput.Focus();
                        YearInput.CaretIndex = 0;
                        e.Handled = true;
                    }
                }
            }
        }
        private void SetupDateFieldsNavigation()
        {
            DayInput.PreviewKeyDown += DateInput_PreviewKeyDown;
            MonthInput.PreviewKeyDown += DateInput_PreviewKeyDown;
            YearInput.PreviewKeyDown += DateInput_PreviewKeyDown;
        }
        private DateTime? GetBirthdayFromInputs()
        {
            if (int.TryParse(DayInput.Text, out int day) &&
                int.TryParse(MonthInput.Text, out int month) &&
                int.TryParse(YearInput.Text, out int year))
            {
                try
                {
                    return new DateTime(year, month, day);
                }
                catch
                {
                    return null;
                }
            }
            return null;
        }

        private DateTime? GetBirthday()
        {
            if (DayInput.Text == DayInput.Tag?.ToString() ||
                MonthInput.Text == MonthInput.Tag?.ToString() ||
                YearInput.Text == YearInput.Tag?.ToString())
            {
                return null;
            }

                        int day, month, year;
            if (!int.TryParse(DayInput.Text, out day) ||
                !int.TryParse(MonthInput.Text, out month) ||
                !int.TryParse(YearInput.Text, out year))
            {
                return null;
            }

                        try
            {
                return new DateTime(year, month, day);
            }
            catch
            {
                return null;
            }
        }
        private void SearchCities(string input)
        {
            if (CitySuggestions == null)
            {
                return;
            }

            CitySuggestions.Items.Clear();
            _foundCities = _cityService.GetCity(input);

            foreach (var city in _foundCities)
            {
                CitySuggestions.Items.Add($"{city.City}, {city.Country}");
            }
            if (_foundCities.Count > 0)
            {
                CitySuggestionsPopup.IsOpen = true;
            }
            else
            {
                CitySuggestionsPopup.IsOpen = false;
            }
        }
        private void NameCity_TextChanged(object sender, TextChangedEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox != null)
            {
                string input = textBox.Text;

                                _isCitySelected = false;

                if (input.Length >= 3 && input != textBox.Tag?.ToString())
                {
                    SearchCities(input);
                }
                else
                {
                    if (CitySuggestionsPopup != null)
                    {
                        CitySuggestionsPopup.IsOpen = false;
                    }
                }
            }
        }


        private string _selectedCity = "";
        private string _selectedCountry = "";
        private bool _isCitySelected = false;

        private void CitySuggestions_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CitySuggestions.SelectedItem != null)
            {
                string selectedItem = CitySuggestions.SelectedItem.ToString();
                NameCity.Text = selectedItem;

                string[] parts = selectedItem.Split(',');
                if (parts.Length >= 2)
                {
                    _selectedCity = parts[0].Trim();
                    _selectedCountry = parts[1].Trim();
                    _isCitySelected = true;
                }
                else
                {
                    _selectedCity = selectedItem;
                    _selectedCountry = "";
                    _isCitySelected = true;
                }

                CitySuggestionsPopup.IsOpen = false;
            }
        }



    }
}