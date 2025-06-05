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
using OxyPlot.Axes;

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
                Logger.Log($"[MainViewModel] OnDataCollected received data. Position: {data.Position}, SensorValue: {data.SensorValue}"); // DIAGNOSTIC LOG
                lock (_bufferedPoints)
                {
                    _bufferedPoints.Add(new DataPoint(data.Position, data.SensorValue));

                    Logger.Log($"[MainViewModel] Buffer now contains {_bufferedPoints.Count} points. Latest position: {data.Position}");
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
                Title = LocalizationHelper.GetLocalizedString("Friction") + "(N)",
                MarkerType = MarkerType.Circle,
                TrackerFormatString = "Position: {2:0.00} mm, Friction: {4:0.00} N" // Enable tooltips
            };
            FrictionPlotModel.Series.Add(lineSeries);
        }

        private const int MaxPointsPerUpdate = 200; // Max points to process in one UI update cycle

        private void UpdateGraph()
        {
            List<DataPoint> pointsToAdd = null; // Initialize to null
            bool hasPointsToProcess = false;
            // The plotNeedsReset logic and individual currentX/Y Min/Max are less critical if axes are always recalculated from the full series.

            lock (_bufferedPoints) // Ensure thread-safety for shared data access
            {
                if (_bufferedPoints.Count > 0) 
                {
                    hasPointsToProcess = true;
                    int count = Math.Min(_bufferedPoints.Count, MaxPointsPerUpdate);
                    pointsToAdd = new List<DataPoint>(_bufferedPoints.GetRange(0, count)); // Assign here
                    _bufferedPoints.RemoveRange(0, count);
                }
                // If _bufferedPoints.Count is 0, hasPointsToProcess remains false, and pointsToAdd remains null.
            }

            if (hasPointsToProcess) // This check implies pointsToAdd is not null due to the logic above
            {
                // Dispatch only the UI update part
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    if (FrictionPlotModel.Series.Count > 0 && FrictionPlotModel.Series[0] is LineSeries lineSeries)
                    {
                        lineSeries.Points.AddRange(pointsToAdd); // Now safe to use pointsToAdd

                        // If there are any points in the series, update axes
                        if (lineSeries.Points.Count > 0)
                        {
                            // More robust axis update: consider all points in the series
                            var overallXMin = lineSeries.Points.Min(p => p.X);
                            var overallXMax = lineSeries.Points.Max(p => p.X);
                            var overallYMin = lineSeries.Points.Min(p => p.Y);
                            var overallYMax = lineSeries.Points.Max(p => p.Y);

                            var xRange = overallXMax - overallXMin;
                            var xMargin = Math.Max(xRange * 0.05, 1.0); // Min margin 1mm
                            FrictionPlotModel.Axes[0].Minimum = overallXMin - xMargin;
                            FrictionPlotModel.Axes[0].Maximum = overallXMax + xMargin;

                            var yRange = overallYMax - overallYMin;
                            var yMargin = Math.Max(yRange * 0.2, 0.1); // Min margin 0.1N
                            FrictionPlotModel.Axes[1].Minimum = overallYMin - yMargin;
                            FrictionPlotModel.Axes[1].Maximum = overallYMax + yMargin;
                        }
                        // else block for plotNeedsReset can be removed if Reset() handles axis reset properly
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
            //if (_testController != null)
            //{
            //    //_testController.StopTest(); // Stop any ongoing test
            //    //_testController.ResetPosition(); // Command to move to position 0
            //}
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
        private readonly object _lockObject = new object(); // Better lock object

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
            // Add X-axis
            FrictionPlotModel.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Bottom,
                Title = LocalizationHelper.GetLocalizedString("Position") + " (mm)"
            });

            // Add Y-axis
            FrictionPlotModel.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Left,
                Title = LocalizationHelper.GetLocalizedString("Friction") + " (N)"
            });
            var lineSeries = new LineSeries
            {
                Title = LocalizationHelper.GetLocalizedString("Friction"),
                MarkerType = MarkerType.Circle,
                TrackerFormatString = "Position: {2:0.00} mm, Friction: {4:0.00} N"
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

        public void ClearData()
        {
            lock (_lockObject)
            {
                _bufferedPoints.Clear();
            }

            App.Current.Dispatcher.Invoke(() =>
            {
                if (FrictionPlotModel.Series.FirstOrDefault() is LineSeries lineSeries)
                {
                    lineSeries.Points.Clear();
                    FrictionPlotModel.InvalidatePlot(true);
                }
            });
        }

        private void UpdateGraph()
        {
            List<DataPoint> pointsToAdd;

            lock (_lockObject)
            {
                if (_bufferedPoints.Count == 0) return;
                pointsToAdd = new List<DataPoint>(_bufferedPoints);
                _bufferedPoints.Clear();
            }

            // Ensure the update happens on the UI thread
            App.Current.Dispatcher.Invoke(() =>
            {
                if (FrictionPlotModel.Series.FirstOrDefault() is LineSeries lineSeries)
                {
                    lineSeries.Points.AddRange(pointsToAdd);

                    // Adjust axes dynamically if there are points
                    if (lineSeries.Points.Count > 0)
                    {
                        // Adjust Y-axis
                        double yMin = lineSeries.Points.Min(p => p.Y);
                        double yMax = lineSeries.Points.Max(p => p.Y);
                        double yMargin = Math.Max((yMax - yMin) * 0.1, 0.1);

                        if (FrictionPlotModel.Axes.Count >= 2)
                        {
                            FrictionPlotModel.Axes[1].Minimum = yMin - yMargin;
                            FrictionPlotModel.Axes[1].Maximum = yMax + yMargin;
                        }

                        // Adjust X-axis
                        double xMin = lineSeries.Points.Min(p => p.X);
                        double xMax = lineSeries.Points.Max(p => p.X);
                        double xMargin = Math.Max((xMax - xMin) * 0.05, 1.0);

                        if (FrictionPlotModel.Axes.Count >= 1)
                        {
                            FrictionPlotModel.Axes[0].Minimum = xMin - xMargin;
                            FrictionPlotModel.Axes[0].Maximum = xMax + xMargin;
                        }
                    }

                    FrictionPlotModel.InvalidatePlot(true);
                }
            });
        }

        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        // Dispose of timer when view model is disposed
        public void Dispose()
        {
            _updateTimer?.Stop();
            _updateTimer?.Dispose();
        }
    }
}

