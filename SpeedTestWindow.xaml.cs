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
    public partial class SpeedTestWindow : Window, IDisposable
    {
        private readonly IMotionController _motionController;
        private readonly SpeedTestViewModel _viewModel;
        private readonly DataAcquisition _dataAcquisition;
        private bool _isSpeedTestRunning = false; // Local flag for test state

        // Pass the motion controller instance from the main window
        public SpeedTestWindow(IMotionController motionController)
        {
            InitializeComponent();
            _motionController = motionController;
            _dataAcquisition = new DataAcquisition(true, motionController);
            _viewModel = new SpeedTestViewModel();
            DataContext = _viewModel;

            // Subscribe to EStop event
            _motionController.OnEStopTriggered += MotionController_OnEStopTriggered;
        }

        private void MotionController_OnEStopTriggered()
        {
            // Ensure this runs on the UI thread of SpeedTestWindow
            this.Dispatcher.Invoke(() =>
            {
                // Check if this window is still visible/active before showing the message
                // to avoid issues if the event comes after the window is closing.
                if (this.IsVisible)
                {
                    MessageBox.Show(this, "Emergency stop triggered during Speed Test! Please reset the E-stop button.", "E-stop (Speed Test)", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            });
        }

        private async void StartTestButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isSpeedTestRunning) // Prevent re-entrancy for this specific test
            {
                MessageBox.Show("Speed test is already in progress.", "Test In Progress", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (TestStateManager.IsTestInProgress) // Global check for any test
            {
                MessageBox.Show("Another test is currently in progress. Please wait.", "Test In Progress", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Parse values from the input controls.
            if (!double.TryParse(StartPositionInput.Text, out double startPos) ||
                !double.TryParse(EndPositionInput.Text, out double endPos) ||
                !int.TryParse(SpeedInput.Text, out int maxVelocity) ||
                !double.TryParse(AccelerationInput.Text, out double acceleration) ||
                !int.TryParse(RepetitionInput.Text, out int repetitions))
            {
                MessageBox.Show("请输入所有有效参数！Invalid argument(s)", "输入错误 Input error(s)", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            Logger.Log($"Speed test started with Start: {startPos}, End: {endPos}, Velocity: {maxVelocity}, " +
                       $"Acceleration: {acceleration}, Repetitions: {repetitions}");

            _isSpeedTestRunning = true; // Set local flag
            TestStateManager.NotifyTestStarted(); // Notify global state
            try
            {
                _motionController._motionCard.GA_AxisOn(1); // Ensure the axis is powered on
                for (int i = 0; i < repetitions; i++)
                {
                    if (!_isSpeedTestRunning) break; // Check if E-stop or close occurred

                    await _motionController.MoveToPositionAsync(startPos * 1000, maxVelocity, acceleration); // 1000 pulses per mm by default
                    if (!_isSpeedTestRunning) break;

                    var moveTask = _motionController.MoveToPositionAsync(endPos * 1000, maxVelocity, acceleration);
                    while (!_motionController.IsMovementDone())
                    {
                        if (!_isSpeedTestRunning) break;
                        double position = _motionController.GetCurrentPosition() / 1000.0; // Convert to mm
                        SensorData data = null;

                        try
                        {
                            data = await _dataAcquisition.CollectDataAtPositionAsync(position);
                            if (data != null)
                            {
                                // Send data to the speed test chart
                                _viewModel.AddDataPoint(data.Position, data.SensorValue);
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.LogException(ex);
                            // Continue motion even if data collection fails
                            continue;
                        }

                        await Task.Delay(10); // Non-blocking delay
                    }
                    if (!_isSpeedTestRunning) break;
                    await moveTask; // Ensure the move task is completed

                    // Optional: Add a small delay between repetitions
                    await Task.Delay(100);
                }
            }
            catch (OperationCanceledException)
            {
                Logger.Log("Speed test operation was cancelled (likely due to E-Stop or window close).");
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
                if (_isSpeedTestRunning) // Only show error if test wasn't intentionally stopped
                {
                   MessageBox.Show("速度测试过程中发生错误，请检查日志。An error occurred during the speed test, please check the logs.", "错误 Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            finally
            {
                if (_isSpeedTestRunning) // If test was running, mark it as completed for TestStateManager
                {
                    TestStateManager.NotifyTestCompleted();
                }
                _isSpeedTestRunning = false; // Clear local flag
                Logger.Log("Speed test completed or exited.");
            }
            // MessageBox.Show("速度测试完成 Speed test completed.", "提示", MessageBoxButton.OK, MessageBoxImage.Information); // Show only on actual completion without error/E-stop
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            if (_isSpeedTestRunning) // If window closed while test was running
            {
                 _motionController.Stop(); // Attempt to stop motor
                 TestStateManager.NotifyTestCompleted(); // Ensure global state is updated
                 _isSpeedTestRunning = false;
                 Logger.Log("SpeedTestWindow closed during an active test. Test marked as completed.");
            }
            Dispose(); // Call Dispose for cleanup
        }

        // Implement IDisposable if you have resources to clean up, like event subscriptions if any were added.
        private bool _disposed = false;
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    if (_motionController != null)
                    {
                        _motionController.OnEStopTriggered -= MotionController_OnEStopTriggered;
                    }
                    // Note: TestStateManager.NotifyTestCompleted() is handled by OnClosed or the test's finally block.
                }
                _disposed = true;
            }
        }
    }
}

