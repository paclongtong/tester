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
using System.Windows.Shapes;

namespace friction_tester
{
    /// <summary>
    /// Interaction logic for Database.xaml
    /// </summary>
    public partial class DatabaseWindow : Window
    {
        private TestResultsRepository _repository;
        public DatabaseWindow()
        {
            InitializeComponent();
            LanguageManager.ChangeLanguage(ConfigManager.Config.SelectedLanguage);
            _repository = new TestResultsRepository();
            LoadTestResults();
        }
        private async void LoadTestResults()
        {
            ResultsDataGrid.ItemsSource = null;
            var results = await _repository.GetAllTestResultsAsync();
            ResultsDataGrid.ItemsSource = results;
        }

        private async void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            string searchTerm = SearchBox.Text.Trim();
            if (!string.IsNullOrEmpty(searchTerm))
            {
                var results = await _repository.GetAllTestResultsAsync();
                if (results != null && results.Any())
                {
                    ResultsDataGrid.ItemsSource = results.Where(r => r.TestName.Contains(searchTerm));
                }
                else
                {
                    MessageBox.Show("未发现目标对象", "查找结果", MessageBoxButton.OK, MessageBoxImage.Information);
                    ResultsDataGrid.ItemsSource = null;
                }
            }
            else
            {
                LoadTestResults();
            }
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            LoadTestResults();
        }

        private async void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (ResultsDataGrid.SelectedItem is TestResult selectedResult)
            {
                var result = MessageBox.Show($"Are you sure you want to delete Test ID: {selectedResult.Id}?",
                                             "Delete Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result == MessageBoxResult.Yes)
                {
                    await _repository.DeleteTestResultAsync(selectedResult.Id);
                    LoadTestResults();
                }
            }
            else
            {
                MessageBox.Show("您正在进行删除操作！如需删除，请选择至少一项", "未选择项目", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void ResultsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        private async void DisplayResultButton_Click(object sender, RoutedEventArgs e)
        {
            if (ResultsDataGrid.SelectedItem is TestResult selectedResult)
            {
                var repository = new TestResultsRepository();
                // Re-fetch the test result with its SensorData included.
                var testResultWithSensorData = await repository.GetTestResultByIdAsync(selectedResult.Id);
                if (testResultWithSensorData == null)
                {
                    MessageBox.Show("Test result not found.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                int count = testResultWithSensorData.SensorData?.Count ?? 0;
                System.Diagnostics.Debug.WriteLine($"TestResult {testResultWithSensorData.Id} has {count} sensor data entries.");
                // Optionally, display a message for debugging:
                if (count == 0)
                {
                    MessageBox.Show("No sensor data loaded! Make sure the relationship is configured correctly.", "Debug Info", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                DisplayResultWindow displayWindow = new DisplayResultWindow(testResultWithSensorData);
                displayWindow.Show();
            }
            else
            {
                MessageBox.Show("Please select a test result to display.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}
