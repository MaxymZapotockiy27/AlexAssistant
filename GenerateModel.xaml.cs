using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Threading.Tasks;
using AlexAssistant.Services;

namespace AlexAssistant
{
    public partial class GenerateModel : Page
    {
        private MainWindow _mainWindow;
        private Storyboard _spinnerAnimation;
        private Storyboard _successAnimation;
        private Storyboard _buttonAppearAnimation;
        private Storyboard _textFadeInAnimation;
        private Storyboard _elementFadeOutAnimation;
        private Storyboard _elementFadeInAnimation;
    
        private bool _isTrainingComplete = false;

        public GenerateModel(MainWindow mainWindow)
        {
            InitializeComponent();
            _mainWindow = mainWindow;
            
            _spinnerAnimation = (Storyboard)FindResource("SpinnerAnimation");
            _successAnimation = (Storyboard)FindResource("SuccessAnimation");
            _buttonAppearAnimation = (Storyboard)FindResource("ButtonAppearAnimation");
            _textFadeInAnimation = (Storyboard)FindResource("TextFadeInAnimation");
            _elementFadeOutAnimation = (Storyboard)FindResource("ElementFadeOutAnimation");
            _elementFadeInAnimation = (Storyboard)FindResource("ElementFadeInAnimation");
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            ShowInitialState();
        }

        private void ShowInitialState()
        {
            _isTrainingComplete = false;
            InfoText.Text = "All snapshots have been successfully taken!";
            TrainModelButton.Content = "Train Model";

            SuccessIconContainer.Visibility = Visibility.Visible;
            _successAnimation.Begin(SuccessIconContainer);

            TrainModelButton.Visibility = Visibility.Visible;
            _buttonAppearAnimation.Begin(TrainModelButton);

            _textFadeInAnimation.Begin(InfoText);
        }

        private async void TrainModelButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isTrainingComplete)
            {
                CloseWindow();
                return;
            }

            TrainModelButton.IsEnabled = false;
            await FadeOutElement(SuccessIconContainer);
            await FadeOutElement(TrainModelButton);
            SuccessIconContainer.Visibility = Visibility.Collapsed;
            TrainModelButton.Visibility = Visibility.Collapsed;

            InfoText.Opacity = 0;
            InfoText.Text = "Training your model. Please wait...";
            _textFadeInAnimation.Begin(InfoText);

            SpinnerContainer.Visibility = Visibility.Visible;
            await FadeInElement(SpinnerContainer);
            _spinnerAnimation.Begin(SpinnerContainer, true);

            await FaceRecogService.TrainModelAsync();

            _spinnerAnimation.Stop(SpinnerContainer);
            await FadeOutElement(SpinnerContainer);
            SpinnerContainer.Visibility = Visibility.Collapsed;

            _isTrainingComplete = true;
            InfoText.Opacity = 0;
            InfoText.Text = "Training completed successfully!";
            _textFadeInAnimation.Begin(InfoText);

            SuccessIconContainer.Visibility = Visibility.Visible;
            _successAnimation.Begin(SuccessIconContainer);

            TrainModelButton.Content = "Close";
            TrainModelButton.Visibility = Visibility.Visible;
            TrainModelButton.IsEnabled = true;
            _buttonAppearAnimation.Begin(TrainModelButton);
        }

        private Task FadeOutElement(UIElement element)
        {
            var tcs = new TaskCompletionSource<bool>();
            var animation = _elementFadeOutAnimation.Clone();
            Storyboard.SetTarget(animation, element);
            animation.Completed += (s, e) => tcs.SetResult(true);
            animation.Begin();
            return tcs.Task;
        }

        private Task FadeInElement(UIElement element)
        {
            var tcs = new TaskCompletionSource<bool>();
            var animation = _elementFadeInAnimation.Clone();
            Storyboard.SetTarget(animation, element);
            animation.Completed += (s, e) => tcs.SetResult(true);
            animation.Begin();
            return tcs.Task;
        }

        private void CloseWindow()
        {
            if (Window.GetWindow(this) is MainWindow mainWindow)
            {
                _mainWindow.NavigateWithFade(new OverlaySetPage(_mainWindow));

            }
            else
            {
                ShowInitialState();
            }
        }
    }
}