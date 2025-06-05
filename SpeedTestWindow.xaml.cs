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
                _isSpeedTestRunning = false; // Explicitly set flag to stop test execution

                // Check if this window is still visible/active before showing the message
                // to avoid issues if the event comes after the window is closing.
                if (this.IsVisible)
                {
                    MessageBox.Show(this, LocalizationHelper.GetLocalizedString("EStopTriggeredSpeedTest"), LocalizationHelper.GetLocalizedString("EStopSpeedTestTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            });
        }

        private async void StartTestButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isSpeedTestRunning) // Prevent re-entrancy for this specific test
            {
                MessageBox.Show(LocalizationHelper.GetLocalizedString("SpeedTestInProgress"), LocalizationHelper.GetLocalizedString("TestInProgressTitle"), MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (TestStateManager.IsTestInProgress) // Global check for any test
            {
                MessageBox.Show(LocalizationHelper.GetLocalizedString("AnotherTestInProgress"), LocalizationHelper.GetLocalizedString("TestInProgressTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Parse values from the input controls.
            if (!double.TryParse(StartPositionInput.Text, out double startPos) ||
                !double.TryParse(EndPositionInput.Text, out double endPos) ||
                !int.TryParse(SpeedInput.Text, out int maxVelocity) ||
                !double.TryParse(AccelerationInput.Text, out double acceleration) ||
                !int.TryParse(RepetitionInput.Text, out int repetitions))
            {
                MessageBox.Show(LocalizationHelper.GetLocalizedString("InvalidSpeedTestParameters"), LocalizationHelper.GetLocalizedString("InputErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            Logger.Log($"Speed test started with Start: {startPos}, End: {endPos}, Velocity: {maxVelocity}, " +
                       $"Acceleration: {acceleration}, Repetitions: {repetitions}");

            bool notifiedStateManagerOfTestStart = false; // Local flag for this test instance

            _isSpeedTestRunning = true; // Set local flag for UI/re-entrancy
            TestStateManager.NotifyTestStarted(); // Notify global state
            notifiedStateManagerOfTestStart = true; // Mark that this instance notified the manager
            try
            {
                _motionController._motionCard.GA_AxisOn(1); // Ensure the axis is powered on
                for (int i = 0; i < repetitions; i++)
                {
                    if (!_isSpeedTestRunning) break; // Check if E-stop or close occurred

                    await _motionController.MoveToPositionAsync(startPos * 1000, maxVelocity, acceleration); // 1000 pulses per mm by default
                    if (!_isSpeedTestRunning) break;

                    var moveTask = _motionController.MoveToPositionAsync(endPos * 1000, maxVelocity, acceleration);
                    
                    // Loop condition now prioritizes _isSpeedTestRunning
                    while (_isSpeedTestRunning && !_motionController.IsMovementDone()) 
                    {
                        // This check is redundant if the while condition includes _isSpeedTestRunning but kept for safety for now.
                        // if (!_isSpeedTestRunning) break; 
                        
                        try
                        {
                            // Attempt to get position and data only if still running
                            double position = _motionController.GetCurrentPosition() / 1000.0; // Convert to mm
                            SensorData data = null;

                            // Existing try-catch for CollectDataAtPositionAsync is fine, 
                            // this outer try-catch handles GetCurrentPosition failure specifically.
                            data = await _dataAcquisition.CollectDataAtPositionAsync(position);
                            if (data != null)
                            {
                                // Send data to the speed test chart
                                _viewModel.AddDataPoint(data.Position, data.SensorValue);
                            }
                        }
                        catch (Exception ex)
                        {
                            // If GetCurrentPosition or CollectDataAtPositionAsync fails, 
                            // especially after an E-Stop (comm loss).
                            Logger.Log($"Error during data collection loop in SpeedTest (iteration {i}): {ex.Message}. Breaking inner loop.");
                            // Critical to break this inner loop if data collection is compromised.
                            break; // Exit the while loop for this segment
                        }
                        await Task.Delay(10); // Non-blocking delay
                    }

                    // After the while loop, check _isSpeedTestRunning before awaiting moveTask
                    // as moveTask might throw OperationCanceledException if E-Stop occurred.
                    if (!_isSpeedTestRunning) 
                    {
                        Logger.Log($"SpeedTestWindow: E-Stop detected after data collection loop (or loop broken by error) for repetition {i}. Breaking repetitions.");
                        break; // Break the outer for-loop (repetitions)
                    }
                    
                    await moveTask; // Ensure the move task is completed (or throws OperationCanceledException)

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
                if (_isSpeedTestRunning) // Only show error if test wasn't intentionally stopped by EStop event handler already
                {
                   MessageBox.Show(LocalizationHelper.GetLocalizedString("SpeedTestExecutionError"), LocalizationHelper.GetLocalizedString("ErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            finally
            {
                if (notifiedStateManagerOfTestStart) // If this instance had notified the manager that a test started
                {
                    TestStateManager.NotifyTestCompleted();
                }
                _isSpeedTestRunning = false; // Clear local UI/re-entrancy flag
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

