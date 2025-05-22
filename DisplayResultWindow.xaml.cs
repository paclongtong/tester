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
using Microsoft.Win32;
using OxyPlot;
using OxyPlot.Series;
using OxyPlot.Wpf;

namespace friction_tester
{
    /// <summary>
    /// Interaction logic for DisplayResultWindow.xaml
    /// </summary>
    public partial class DisplayResultWindow : Window
    {
        private TestResult _testResult;
        private PlotModel _plotModel;

        public DisplayResultWindow(TestResult testResult)
        {
            InitializeComponent();
            _testResult = testResult;
            BuildPlot();
        }

        private void BuildPlot()
        {
            // Log the count for debugging purposes:
            int count = _testResult.SensorData?.Count ?? 0;
            System.Diagnostics.Debug.WriteLine($"TestResult {_testResult.Id} has {count} sensor data entries.");

            if (count == 0)
            {
                MessageBox.Show("No sensor data found for this test result.", "Debug Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Create a new plot model with the test result's name as the title.
            _plotModel = new PlotModel { Title = _testResult.TestName };

            // Create a line series to represent the sensor data.
            var series = new LineSeries
            {
                Title = "Sensor Data",
                MarkerType = MarkerType.Circle
            };

            // Order the sensor data (by Timestamp or Position as appropriate)
            var sensorData = _testResult.SensorData.OrderBy(sd => sd.Timestamp);
            _plotModel.Series.Add(series);
            foreach (var data in sensorData)
            {
                series.Points.Add(new DataPoint(data.Position, data.SensorValue));
            }

            
            PlotView.Model = _plotModel;
            _plotModel.InvalidatePlot(true);
        }

        private void OutputImageButton_Click(object sender, RoutedEventArgs e)
        {
            // Let the user choose the file path for the PNG image.
            SaveFileDialog dlg = new SaveFileDialog
            {
                Filter = "PNG Image|*.png",
                Title = "Save Plot as Image",
                FileName = "TestResultPlot.png"
            };

            if (dlg.ShowDialog() == true)
            {
                string filename = dlg.FileName;
                // Use the OxyPlot PNG exporter.
                _plotModel.Background = OxyColors.White;
                var pngExporter = new OxyPlot.Wpf.PngExporter
                {
                    Width = 800,
                    Height = 600,
                    //Background = OxyColors.White
                };
                pngExporter.ExportToFile(_plotModel, filename);
                MessageBox.Show($"Plot saved to {filename}", "Export Successful", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }
}
