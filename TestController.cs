using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Animation;
using System.Windows;

namespace friction_tester
{
    public class TestController
    {
        public event Action<SensorData> OnDataCollected;

        internal IMotionController _motorController;
        public DataAcquisition _dataAcquisition;
        private CancellationTokenSource _cancellationTokenSource;
        private CancellationTokenSource _moveCancellationTokenSource;
        private SensorData _lastSensorDataSent; // Field to store the last sent data

        private readonly TestResultsRepository _repository;
        public event Action OnTestStarted;
        public event Action OnTestCompleted;
        public TestResult TestResult { get; private set; }
        public TestController(bool isSimulationMode)
        {
            _motorController = MotionControllerFactory.CreateController(isSimulationMode);
            _motorController.Initialize();
            _dataAcquisition = new DataAcquisition(isSimulationMode, _motorController);
            _dataAcquisition.OnDataCollected += (data) =>
            {
                // Check if the new data is different from the last one sent
                // This simple check assumes Timestamp is granular enough or Position/SensorValue changes are significant.
                // For more robustness, you might need a more sophisticated comparison if timestamps are too close for identical data.
                if (_lastSensorDataSent == null || 
                    data.Timestamp != _lastSensorDataSent.Timestamp || 
                    data.Position != _lastSensorDataSent.Position || 
                    data.SensorValue != _lastSensorDataSent.SensorValue)
                {
                    OnDataCollected?.Invoke(data);
                    _lastSensorDataSent = data; // Update the last sent data
                }
                // Else, data is considered a duplicate of the last one, so we don't re-invoke.
            };
            _cancellationTokenSource = new CancellationTokenSource();
            _repository = new TestResultsRepository();
        }

        public async Task StartAutomaticTest(double speed, double acceleration, double x1, double x2, string workpieceName)
        {
            if (TestStateManager.IsTestInProgress || _motorController.IsJoystickMode)
            {
                Logger.Log("Automatic test start blocked: Another test is in progress or Handwheel mode is active.");
                MessageBox.Show(LocalizationHelper.GetLocalizedString("TestOrHandwheelActive"));
                return;
            }

            TestStateManager.NotifyTestStarted();
            OnTestStarted?.Invoke();

            var testName = $"{workpieceName}_{DateTime.Now:yyyyMMddHHmmss}";

            _moveCancellationTokenSource = new CancellationTokenSource();
            try
            {
                Logger.Log($"Moving to start position: {x1}mm");
                await _motorController.MoveToPositionAsync(x1 * 1000, (int)speed, acceleration, _moveCancellationTokenSource.Token); // question
                _motorController.SetLightOutput("green");
                Logger.Log($"Moving to end position: {x2}mm with data collection");
                var moveTask = _motorController.MoveToPositionAsync(x2 * 1000, (int)speed, acceleration, _moveCancellationTokenSource.Token);
                while (!_motorController.IsMovementDone())
                {
                    if (_moveCancellationTokenSource.Token.IsCancellationRequested)
                        break;
                    double position = _motorController.GetCurrentPosition() / 1000;
                    SensorData data = null;
                    try
                    {
                        data = await _dataAcquisition.CollectDataAtPositionAsync(position);
                        if (data != null)
                            OnDataCollected?.Invoke(data);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogException(ex); // Log the issue
                                                    // Optionally: You can also log to the UI, set a warning flag, etc.
                        continue; // Skip this data point and continue motion
                    }
                    await Task.Delay(10); // Non-blocking delay
                }
                await moveTask; // Ensure the move task is completed
                var sensorDataList = _dataAcquisition.GetBuffer();
                await StoreTestResultAsync(sensorDataList, testName, speed, acceleration, x1, x2, workpieceName);
                _motorController.SetLightOutput("yellow");
                Logger.Log($"Test completed successfully: {testName}");
                _dataAcquisition.ClearBuffer();
            }
            catch (OperationCanceledException oce) // Catch OperationCanceledException specifically
            {
                // This is an expected cancellation if StopTest or EStop was called.
                Logger.Log($"Automatic test operation was cancelled: {oce.Message}");
                // No MessageBox here for OperationCanceledException, as this is often a controlled stop.
                // Do NOT re-throw, to prevent MainWindow from showing another error message for a controlled stop.
            }
            catch (Exception ex) // Catch other unexpected exceptions
            {
                MessageBox.Show(string.Format(LocalizationHelper.GetLocalizedString("AutoModeErrorCheckLogs"), ex.Message), LocalizationHelper.GetLocalizedString("ErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
                Logger.LogException(ex);
                throw; // Rethrow other unexpected exceptions to be handled by the caller (e.g., MainWindow)
            }
            finally
            {
                _moveCancellationTokenSource?.Dispose();
                _moveCancellationTokenSource = null;
                OnTestCompleted?.Invoke();
                TestStateManager.NotifyTestCompleted();
            }

        }

        public async Task ResetPosition()
        {
            Logger.Log("[TestController] ResetPosition called.");
            
            try
            {
                // Check if we need to reset axis after emergency stop
                var realController = _motorController as RealMotionController;
                if (realController != null)
                {
                    // Check if axis is in alarm state or disabled
                    if (realController.IsAxisInAlarm())
                    {
                        Logger.Log("[TestController] Axis appears to be in alarm state, resetting...");
                        await realController.ResetAxisAfterEStopAsync();
                    }
                }

                Logger.Log("[TestController] ResetPosition: Attempted to clear axis alarm.");

                double speed = ConfigManager.Config.Axes[0].HomeReturnSpeed;
                double acceleration = 20; // Default acceleration for reset
                Logger.Log($"[TestController] ResetPosition: Speed={speed}, Acceleration={acceleration}. Moving to position 0.");
                _motorController.SetLightOutput("green"); // Light Green
                using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60))) // 60 second timeout
                {
                    await _motorController.MoveToPositionAsync(0, 50, 1000, cts.Token);
                }
                Logger.Log("[TestController] ResetPosition: MoveToPositionAsync(0) completed.");
                _motorController.SetLightOutput("yellow"); // Light yellow
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
                _motorController.HandleExternalInput(0);
                throw;
            }

        }
        public Task StopTestAsync()
        {
            // offload to background so we don't block UI thread
            return Task.Run(() => StopTest());
        }
        public void StopTest()
        {
            try
            {
                if (_cancellationTokenSource != null)
                {
                    _cancellationTokenSource.Cancel();
                    _cancellationTokenSource.Dispose();
                    _cancellationTokenSource = new CancellationTokenSource();
                }
                _moveCancellationTokenSource?.Cancel();
                _motorController.Stop();
                _motorController.SetLightOutput("yellow");
                _dataAcquisition.ClearBuffer();
                Logger.Log("Emergency stop triggered. Motion halted. Test stopped and buffer cleared.\r " +
                    "紧急停止触发，测试停止");
            }
            catch (Exception ex)
            {
                //Console.WriteLine($"停止测试时出错， 错误信息：{ex.Message}");
                Logger.LogException(ex);
            }

        }

        public IMotionController GetMotionController()
        {
            return _motorController;
        }

        public async Task StoreTestResultAsync(
            List<SensorData> sensorDataList,
            string testName,
            double speed,
            double acceleration,
            double x1,
            double x2,
            string workpieceName)
        {
            if (sensorDataList == null || !sensorDataList.Any())
            {
                Logger.Log("Sensor data list is empty. Aborting test result storage.");
                MessageBox.Show(LocalizationHelper.GetLocalizedString("SensorDataEmptyNoSave"), LocalizationHelper.GetLocalizedString("DataErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Step 1: Create and save the TestResult entity first
            TestResult = new TestResult
            {
                TestName = testName,
                Velocity = (float)speed,
                Acceleration = (float)acceleration,
                StartPosition = (float)x1,
                EndPosition = (float)x2,
                DistanceCovered = (float)(x2 - x1),
                TotalDuration = sensorDataList.Max(sd => (sd.Timestamp - sensorDataList.Min(d => d.Timestamp)).TotalSeconds),
                //AdditionalData = null,
                WorkpieceName = workpieceName
            };

            await _repository.AddTestResultAsync(TestResult);

            // Step 2: Set the correct TestId in SensorData entries
            foreach (var sensorData in sensorDataList)
            {
                sensorData.TestId = TestResult.Id; // Link sensor data to the saved TestResult
            }

            // Step 3: Save SensorData entries separately
            await _repository.AddSensorDataAsync(sensorDataList);
        }


        public async Task StoreTestResultAsync_Deprecated(
            List<SensorData> sensorDataList,
            string testName,
            double speed,
            double acceleration,
            double x1,
            double x2,
            string workpieceName)
        {
            if (sensorDataList == null || !sensorDataList.Any())
            {
                Logger.Log("Sensor data list is empty. Aborting test result storage.");
                MessageBox.Show(LocalizationHelper.GetLocalizedString("SensorDataEmptyNoSave"), LocalizationHelper.GetLocalizedString("DataErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var testResult = new TestResult
            {
                TestName = testName,
                Velocity = (float)speed,
                Acceleration = (float)acceleration,
                StartPosition = (float)x1,
                EndPosition = (float)x2,
                DistanceCovered = (float)(x2 - x1),
                TotalDuration = sensorDataList.Max(sd => (sd.Timestamp - sensorDataList.Min(d => d.Timestamp)).TotalSeconds),
                //AdditionalData = null, // Add metadata if needed
                SensorData = sensorDataList, // Link the sensor data directly
                WorkpieceName = workpieceName
            };

            await _repository.AddTestResultAsync(testResult);
        }
    }
}

