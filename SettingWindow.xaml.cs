using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace friction_tester
{
    /// <summary>
    /// Interaction logic for SettingWindow.xaml
    /// </summary>
    public partial class SettingWindow : Window
    {
        private AppConfig _config;
        private const double PULSES_PER_MM = 1000.0; // 1000 pulses per mm
        private const int AXIS_NUMBER = 1; // Assuming axis 1, adjust as needed
        public SettingWindow()
        {       
            InitializeComponent();     
            //_config = ConfigManager.Config; // Load configuration from file
            // Ensure config is loaded - defensive approach
            if (ConfigManager.Config == null)
            {
                ConfigManager.LoadConfig(); // Load if not already loaded
            }

            _config = ConfigManager.Config;

            // Additional safety check
            if (_config == null)
            {
                MessageBox.Show("Failed to load configuration. Using default settings.",
                               "Configuration Warning",
                               MessageBoxButton.OK,
                               MessageBoxImage.Warning);
                _config = new AppConfig { Axes = new List<AxisConfig> { new AxisConfig() } };
            }
            //AppConfig config = ConfigManager.LoadConfig();
            LanguageManager.ChangeLanguage(ConfigManager.Config.SelectedLanguage);
            LoadSettingsToUI();
            //this.Language = System.Windows.Markup.XmlLanguage.GetLanguage(Thread.CurrentThread.CurrentUICulture.Name);

        }
        private void NumericInput_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // Allow only numeric input (including decimal point)
            Regex regex = new Regex(@"^[0-9]*(\.[0-9]*)?$");
            e.Handled = !regex.IsMatch(e.Text);
        }

        private void LoadSettingsToUI()
        {
            //if (_config.Axes.Count > 0)
            //{
            //    SoftLimitMinTextBox.Text = _config.Axes[0].SoftLimitMin.ToString();
            //    SoftLimitMaxTextBox.Text = _config.Axes[0].SoftLimitMax.ToString();
            //}
            //if (_config.Axes == null || _config.Axes.Count == 0)
            //{
            //    MessageBox.Show("Configuration file is empty or corrupted. Default settings will be used.",
            //                    "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            //    _config.Axes.Add(new AxisConfig());  // Ensure there's at least one axis configuration
            //}

            //// Convert from pulses back to mm for display
            //double softLimitMinMm = _config.Axes[0].SoftLimitMin / PULSES_PER_MM;
            //double softLimitMaxMm = _config.Axes[0].SoftLimitMax / PULSES_PER_MM;

            //SoftLimitMinTextBox.Text = softLimitMinMm.ToString("F3"); // 3 decimal places
            //SoftLimitMaxTextBox.Text = softLimitMaxMm.ToString("F3"); // 3 decimal places

            //IsHardLimitEnabledCheckBox.IsChecked = _config.Axes[0].IsHardLimitEnabled;

            if (_config.Axes == null || _config.Axes.Count == 0)
            {
                MessageBox.Show("Configuration file is empty or corrupted. Default settings will be used.",
                                "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                _config.Axes = new List<AxisConfig>();
                _config.Axes.Add(new AxisConfig());  // Ensure there's at least one axis configuration
            }

            try
            {
                // The config already stores values in mm (as doubles), so display directly
                SoftLimitMinTextBox.Text = _config.Axes[0].SoftLimitMin.ToString("F3"); // 3 decimal places
                SoftLimitMaxTextBox.Text = _config.Axes[0].SoftLimitMax.ToString("F3"); // 3 decimal places
                IsHardLimitEnabledCheckBox.IsChecked = _config.Axes[0].IsHardLimitEnabled;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading settings: {ex.Message}. Using default values.",
                                "Error", MessageBoxButton.OK, MessageBoxImage.Warning);

                // Set default values in case of error
                SoftLimitMinTextBox.Text = "0.000";
                SoftLimitMaxTextBox.Text = "100.000";
                IsHardLimitEnabledCheckBox.IsChecked = false;
            }
        }

        private void SaveSettings_Click(object sender, RoutedEventArgs e)
        {
            //ClearAllTextBoxes();

            try
            {
                // Parse and validate soft limit inputs
                if (!double.TryParse(SoftLimitMinTextBox.Text, out double softLimitMinMm))
                {
                    MessageBox.Show("Invalid soft limit minimum value. Please enter a valid number.",
                                    "Input Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (!double.TryParse(SoftLimitMaxTextBox.Text, out double softLimitMaxMm))
                {
                    MessageBox.Show("Invalid soft limit maximum value. Please enter a valid number.",
                                    "Input Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Validate that min < max
                if (softLimitMinMm >= softLimitMaxMm)
                {
                    MessageBox.Show("Soft limit minimum must be less than maximum.",
                                    "Input Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Convert mm to pulses for storage
                long softLimitMinPulses = (long)(softLimitMinMm * PULSES_PER_MM);
                long softLimitMaxPulses = (long)(softLimitMaxMm * PULSES_PER_MM);

                // Check if values are within acceptable range for your motion controller
                const long MAX_PULSE_VALUE = 2147483647; // int.MaxValue
                const long MIN_PULSE_VALUE = -2147483648; // int.MinValue

                if (softLimitMinPulses < MIN_PULSE_VALUE || softLimitMinPulses > MAX_PULSE_VALUE)
                {
                    MessageBox.Show($"Soft limit minimum value ({softLimitMinMm:F3} mm) results in pulse value outside acceptable range.",
                                    "Value Out of Range", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (softLimitMaxPulses < MIN_PULSE_VALUE || softLimitMaxPulses > MAX_PULSE_VALUE)
                {
                    MessageBox.Show($"Soft limit maximum value ({softLimitMaxMm:F3} mm) results in pulse value outside acceptable range.",
                                    "Value Out of Range", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Get hard limit setting
                bool isHardLimitEnabled = IsHardLimitEnabledCheckBox.IsChecked ?? false;

                // Save to configuration (values will be applied on next startup)
                _config.Axes[0].SoftLimitMin = (int)softLimitMinPulses;
                _config.Axes[0].SoftLimitMax = (int)softLimitMaxPulses;
                _config.Axes[0].IsHardLimitEnabled = isHardLimitEnabled;

                // Save configuration to file
                ConfigManager.SaveConfig();

                MessageBox.Show("Settings saved successfully! Changes will take effect after application restart.",
                                "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving settings: {ex.Message}",
                                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            ClearAllTextBoxes(); // Clear all text boxes after saving
        }
        private void ClearAllTextBoxes()
        {
            foreach (var control in FindVisualChildren<TextBox>(this))
            {
                control.Text = string.Empty;
            }
        }


        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj != null)
            {
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
                {
                    DependencyObject child = VisualTreeHelper.GetChild(depObj, i);
                    if (child is T typedChild)
                    {
                        yield return typedChild;
                    }

                    foreach (T childOfChild in FindVisualChildren<T>(child))
                    {
                        yield return childOfChild;
                    }
                }
            }
        }

        private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LanguageComboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                string languageCode = selectedItem.Tag.ToString();
                //AppConfig config = ConfigManager.LoadConfig();
                ConfigManager.Config.SelectedLanguage = languageCode;
                ConfigManager.SaveConfig();

                // Apply language change immediately
                LanguageManager.ChangeLanguage(languageCode);
            }
        }

        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {

        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {

        }

    }
}
