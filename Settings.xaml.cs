using AlexAssistant.Models;
using DirectShowLib;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace AlexAssistant
{
    
    public partial class Settings : Page
    {
        private List<MicroModel> _microChoices = new List<MicroModel>();
        private List<CameraModel> _cameraChoices = new List<CameraModel>();

        public MainWindow _mainWindow;
        public Settings(MainWindow mainWindow)
        {
            InitializeComponent();
            _mainWindow = mainWindow;
            InitializeMicroCamera();


        }
        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
           CameraComboBox.SelectedValue= Properties.Settings.Default.CameraIndex;
           MicrophoneComboBox.SelectedValue = Properties.Settings.Default.MicrophoneIndexSet;
        }
        private void InitializeMicroCamera()
        {

            _microChoices.Clear();
            _cameraChoices.Clear();
            DsDevice[] videoDevices = DsDevice.GetDevicesOfCat(FilterCategory.VideoInputDevice);
            for (int i = 0; i < videoDevices.Length; i++)
            {
                _cameraChoices.Add(new CameraModel {
                    CameraName = videoDevices[i].Name,
                    id = i
                });
            }
            for (int i = 0; i < WaveIn.DeviceCount; i++)
            {

                var deviceInfo = WaveIn.GetCapabilities(i);
                _microChoices.Add(new MicroModel
                {
                    MicroName = deviceInfo.ProductName,
                    id=i
                });
            }
            MicrophoneComboBox.ItemsSource = _microChoices;
            MicrophoneComboBox.DisplayMemberPath = "MicroName";

            CameraComboBox.ItemsSource = _cameraChoices;
            CameraComboBox.DisplayMemberPath = "CameraName";
            MicrophoneComboBox.SelectedValue = Properties.Settings.Default.MicrophoneIndexSet;
            CameraComboBox.SelectedValue = Properties.Settings.Default.CameraIndex;
        }
        private void MicrophoneComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (MicrophoneComboBox.SelectedItem is MicroModel selectedMicro)
            {
                Properties.Settings.Default.MicrophoneIndexSet = selectedMicro.id;
                Properties.Settings.Default.MicrophoneName = selectedMicro.MicroName;
                Properties.Settings.Default.Save(); 
            }
        }

        private void CameraComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CameraComboBox.SelectedItem is CameraModel selectedCamera)
            {
                Properties.Settings.Default.CameraIndex = selectedCamera.id;
                Properties.Settings.Default.Save(); 
            }
        }

    }
}
