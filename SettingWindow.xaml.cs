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
        public SettingWindow()
        {
            InitializeComponent();
            _config = ConfigManager.Config; // Load configuration from file

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
            if (_config.Axes == null || _config.Axes.Count == 0)
            {
                MessageBox.Show("Configuration file is empty or corrupted. Default settings will be used.",
                                "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                _config.Axes.Add(new AxisConfig());  // Ensure there's at least one axis configuration
            }

            SoftLimitMinTextBox.Text = _config.Axes[0].SoftLimitMin.ToString();
            SoftLimitMaxTextBox.Text = _config.Axes[0].SoftLimitMax.ToString();

            IsHardLimitEnabledCheckBox.IsChecked = _config.Axes[0].IsHardLimitEnabled;
        }

        private void SaveSettings_Click(object sender, RoutedEventArgs e)
        {
            ClearAllTextBoxes();
            MessageBox.Show("Settings saved", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
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
