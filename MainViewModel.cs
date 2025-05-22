using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using OxyPlot;
using OxyPlot.Series;
using CommunityToolkit.Mvvm.Input;

namespace friction_tester
{
    public class MainViewModel : INotifyPropertyChanged
    {        
        public event PropertyChangedEventHandler PropertyChanged;
        private readonly System.Timers.Timer _updateTimer;
        // Buffer for temporarily storing data points before adding them to the graph
        private readonly List<DataPoint> _bufferedPoints = new();
        private TestController _testController;

        private PlotModel _frictionPlotModel;
        public PlotModel FrictionPlotModel
        {
            get => _frictionPlotModel;
            set {
                _frictionPlotModel = value;
                OnPropertyChanged(nameof(FrictionPlotModel));
            }

        }

        private AppConfig _config;
        public AppConfig Config
        {
            get => _config;
            set
            {
                _config = value;
                OnPropertyChanged(nameof(Config));
            }
        }

        public MainViewModel(TestController testController)
        {
            _testController = testController;
            _testController.OnDataCollected += data =>
            {
                lock (_bufferedPoints)
                {
                    _bufferedPoints.Add(new DataPoint(data.Position, data.SensorValue));
                }
            };
            InitializePlot();
            // Initialize the timer
            _updateTimer = new System.Timers.Timer(100); // Update every 100ms
            _updateTimer.Elapsed += (s, e) => UpdateGraph();
            _updateTimer.Start();

            //Config = ConfigManager.LoadConfig();
            
        }

        public ICommand SaveCommand => new RelayCommand(() => ConfigManager.SaveConfig(Config));
        private void InitializePlot()
        {
            FrictionPlotModel = new PlotModel() { Title = LocalizationHelper.GetLocalizedString("FrictionDisplacementChart") };
            //var lineSeries = new LineSeries { Title = "摩擦力", MarkerType = MarkerType.Circle };
            var xAxis = new OxyPlot.Axes.LinearAxis
            {
                Position = OxyPlot.Axes.AxisPosition.Bottom,
                Title = "Position",
                IsPanEnabled = true,
                IsZoomEnabled = true
            };

            var yAxis = new OxyPlot.Axes.LinearAxis
            {
                Position = OxyPlot.Axes.AxisPosition.Left,
                Title = "Friction",
                IsPanEnabled = true,
                IsZoomEnabled = true
            };

            FrictionPlotModel.Axes.Add(xAxis);
            FrictionPlotModel.Axes.Add(yAxis);

            var lineSeries = new LineSeries
            {
                Title = LocalizationHelper.GetLocalizedString("Friction"),
                MarkerType = MarkerType.Circle,
                TrackerFormatString = "X: {2:0.00}, Y: {4:0.00}" // Enable tooltips
            };
            FrictionPlotModel.Series.Add(lineSeries);
        }

        private void UpdateGraph()
        {
            lock (_bufferedPoints) // Ensure thread-safety for shared data access
            {
                if (_bufferedPoints.Count == 0) return; // Exit if no new data points

                // Dispatcher is used to update the UI thread safely
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    // Add buffered points to the series in the FrictionPlotModel
                    if (FrictionPlotModel.Series.Count > 0 && FrictionPlotModel.Series[0] is LineSeries lineSeries)
                    {
                        lineSeries.Points.AddRange(_bufferedPoints);

                        // Adjust Y-axis dynamically with smoother scaling
                        var yMin = lineSeries.Points.Min(p => p.Y);
                        var yMax = lineSeries.Points.Max(p => p.Y);
                        var range = yMax - yMin;
                        var margin = Math.Max(range * 0.2, 0.1); // Ensure a minimum margin
                        FrictionPlotModel.Axes[1].Minimum = yMin - margin;
                        FrictionPlotModel.Axes[1].Maximum = yMax + margin;

                        _bufferedPoints.Clear(); // Clear the buffer after processing
                        FrictionPlotModel.InvalidatePlot(true); // Refresh the plot
                    }
                }, System.Windows.Threading.DispatcherPriority.Background);
            }
        }
        public void Reset()
        {
            lock (_bufferedPoints)
            {
                // Clear buffered data
                _bufferedPoints.Clear();

                // Clear and reset the LineSeries
                if (FrictionPlotModel.Series.Count > 0 && FrictionPlotModel.Series[0] is LineSeries lineSeries)
                {
                    lineSeries.Points.Clear();
                }

                // Reset axes to default ranges
                foreach (var axis in FrictionPlotModel.Axes)
                {
                    axis.Reset();
                }

                // Refresh the plot
                FrictionPlotModel.InvalidatePlot(true);
            }

            // Move the testing head back to the origin (assuming origin is 0.0)
            if (_testController != null)
            {
                //_testController.StopTest(); // Stop any ongoing test
                _testController.ResetPosition(); // Command to move to position 0
            }
        }

        protected void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}

namespace friction_tester
{
    public class SpeedTestViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private PlotModel _frictionPlotModel;
        public PlotModel FrictionPlotModel
        {
            get => _frictionPlotModel;
            set { _frictionPlotModel = value; OnPropertyChanged(nameof(FrictionPlotModel)); }
        }

        // Buffer for temporarily storing sensor data points.
        private readonly List<DataPoint> _bufferedPoints = new();
        private System.Timers.Timer _updateTimer;

        public SpeedTestViewModel()
        {
            InitializePlot();
            _updateTimer = new System.Timers.Timer(100); // update every 100ms
            _updateTimer.Elapsed += (s, e) => UpdateGraph();
            _updateTimer.Start();
        }

        private void InitializePlot()
        {
            FrictionPlotModel = new PlotModel { Title = LocalizationHelper.GetLocalizedString("FrictionDisplacementChart") };
            var lineSeries = new LineSeries
            {
                Title = LocalizationHelper.GetLocalizedString("Friction"),
                MarkerType = MarkerType.Circle,
                TrackerFormatString = "X: {2:0.00}, Y: {4:0.00}"
            };
            FrictionPlotModel.Series.Add(lineSeries);
        }

        public void AddDataPoint(double position, double friction)
        {
            lock (_bufferedPoints)
            {
                _bufferedPoints.Add(new DataPoint(position, friction));
            }
        }

        private void UpdateGraph()
        {
            lock (_bufferedPoints)
            {
                if (_bufferedPoints.Count == 0) return;

                // Ensure the update happens on the UI thread.
                App.Current.Dispatcher.Invoke(() =>
                {
                    if (FrictionPlotModel.Series.FirstOrDefault() is LineSeries lineSeries)
                    {
                        lineSeries.Points.AddRange(_bufferedPoints);

                        // Adjust Y-axis dynamically
                        double yMin = lineSeries.Points.Min(p => p.Y);
                        double yMax = lineSeries.Points.Max(p => p.Y);
                        double margin = Math.Max((yMax - yMin) * 0.2, 0.1);
                        if (FrictionPlotModel.Axes.Count >= 2)
                        {
                            FrictionPlotModel.Axes[1].Minimum = yMin - margin;
                            FrictionPlotModel.Axes[1].Maximum = yMax + margin;
                        }
                        _bufferedPoints.Clear();
                        FrictionPlotModel.InvalidatePlot(true);
                    }
                });
            }
        }

        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

