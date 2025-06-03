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
                OnDataCollected?.Invoke(data);
            };
            _cancellationTokenSource = new CancellationTokenSource();
            _repository = new TestResultsRepository();
        }

        public async Task StartAutomaticTest(double speed, double acceleration, double x1, double x2, string workpieceName)
        {
            if (_motorController.IsHandwheelMode)
            {
                Logger.Log("Automatic test start blocked: Handwheel mode is active.");
                MessageBox.Show("自动测试开始被阻止：手轮模式已激活，请先关闭手轮模式 | Disable Handwheel first");
                return;
            }

            OnTestStarted?.Invoke(); // Notify UI that test is starting

            var testName = $"{workpieceName}_{DateTime.Now:yyyyMMddHHmmss}";

            _moveCancellationTokenSource = new CancellationTokenSource();
            try
            {
                Logger.Log($"Moving to start position: {x1}mm");
                await _motorController.MoveToPositionAsync(x1 * 1000, (int)speed, acceleration, _moveCancellationTokenSource.Token); // question
                _motorController.HandleExternalInput(1);
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
                _motorController.HandleExternalInput(2);
                Logger.Log($"Test completed successfully: {testName}");
                _dataAcquisition.ClearBuffer();
            }
            catch (Exception ex)
            {
                MessageBox.Show("自动模式运行时出错，请检查日志 Error occurred, please check logs", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                Logger.LogException(ex);
                //OnTestCompleted?.Invoke();
                throw; // Rethrow the exception to be handled by the caller
            }
            finally
            {
                _moveCancellationTokenSource?.Dispose();
                _moveCancellationTokenSource = null;
                OnTestCompleted?.Invoke();
            }

        }

        public Task ResetPosition()
        {
            return Task.Run(async () =>
            {
                _motorController.HandleExternalInput(2);
                double speed = ConfigManager.Config.Axes[0].HomeReturnSpeed;
                double acceleration = 20;
                _motorController.HandleExternalInput(1);
                _moveCancellationTokenSource = new CancellationTokenSource();
                await _motorController.MoveToPositionAsync(0, (int)speed, acceleration, _moveCancellationTokenSource.Token);
                _motorController.HandleExternalInput(2);
            });
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
                _motorController.HandleExternalInput(2);
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
                MessageBox.Show("Sensor data list is empty. Test result not saved.", "Data Error", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                MessageBox.Show("Sensor data list is empty. Test result not saved.", "Data Error", MessageBoxButton.OK, MessageBoxImage.Warning);
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

