using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Ookii.Dialogs.Wpf;
using System.IO;

using System;
using System.Windows;
using OxyPlot;
using OxyPlot.Series;
using MultiCardCS;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using OxyPlot.Wpf;

namespace friction_tester
{

    public partial class MainWindow : Window
    {
        private TestController _testController;
        private LineSeries _frictionSeries;

        public PlotModel FrictionPlotModel { get; private set; }
        public IMotionController _motorController;
        private bool _isTestRunning = false;
        private DatabaseWindow _databaseWindow;
        private SettingWindow _settingWindow;
        short _axisNumber = 1;
        private bool _isHandwheelOn = false; // Track the state of Handwheel mode
        private bool _isJoystickOn = false; // Track the state of Joystick mode

        private bool _isSimulation = false; // Set to true if Motion Control Card is missing

        // New Properties for the fields
        private string _guideModel;
        private string _preload;
        private string _sealLubrication;

        public string GuideModel
        {
            get => _guideModel;
            set
            {
                var cleanedValue = value.Trim(); // Remove leading/trailing spaces
                if (!Regex.IsMatch(cleanedValue, @"^[a-zA-Z0-9]*$")) // Check for invalid characters
                {
                    MessageBox.Show("Guide Model can only contain letters and numbers.", "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                _guideModel = cleanedValue.ToUpper(); // Convert to uppercase
            }
        }

        public string Preload
        {
            get => _preload;
            set
            {
                var cleanedValue = value.Trim(); // Remove leading/trailing spaces
                if (!Regex.IsMatch(cleanedValue, @"^[a-zA-Z0-9]*$")) // Check for invalid characters
                {
                    MessageBox.Show("Preload can only contain letters and numbers.", "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                _preload = cleanedValue.ToUpper(); // Convert to uppercase
            }
        }

        public string SealLubrication
        {
            get => _sealLubrication;
            set
            {
                var cleanedValue = value.Trim(); // Remove leading/trailing spaces
                if (!Regex.IsMatch(cleanedValue, @"^[a-zA-Z0-9]*$")) // Check for invalid characters
                {
                    MessageBox.Show("Seal Lubrication can only contain letters and numbers.", "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                _sealLubrication = cleanedValue.ToUpper(); // Convert to uppercase
            }
        }

        public MainWindow()
        {
            InitializeComponent();
            InitializePlot();

            _isHandwheelOn = true;
            //HandwheelButton.Content = "手轮模式：关";

            //AppConfig config = ConfigManager.LoadConfig(); // Load settings on startup
            LanguageManager.ChangeLanguage(ConfigManager.Config.SelectedLanguage);
            //ApplyConfig(config); // Apply settings to UI


            Logger.Log("Application started.");
            LogActiveSerialPorts();
            LogConnectedIPs();

            _testController = new TestController(isSimulationMode: _isSimulation);       // Set the simulation mode True if Motion Control Card is missing
            _motorController = _testController.GetMotionController();
            _motorController.HandleExternalInput(2);
            _testController.OnDataCollected += UpdateFrictionChart;
            DataContext = new MainViewModel(_testController);

            LoadConfigOnStartup();

            _testController.OnTestStarted += () =>
            {
                _isTestRunning = true;
                Dispatcher.Invoke(() =>
                {
                    StartAutoButtonFriction.IsEnabled = false;
                });
            };

            _testController.OnTestCompleted += () =>
            {
                _isTestRunning = false;
                Dispatcher.Invoke(() =>
                {
                    StartAutoButtonFriction.IsEnabled = true;
                });
            };

            //_motorController.OnEStopTriggered += () =>
            //{
            //    Dispatcher.Invoke(() =>
            //    {
            //        MessageBox.Show("Emergency stop triggered! Please reset the E-stop button before resuming.", "E-stop", MessageBoxButton.OK, MessageBoxImage.Warning);
            //    });
            //};

        }

        private void LoadConfigOnStartup()
        {
            //AppConfig config = ConfigManager.LoadConfig();
            ApplyConfig(ConfigManager.Config);
        }

        private void ApplyConfig(AppConfig config)
        {
            if (!_isSimulation)
            {
                if (config.Axes != null && config.Axes.Count > 0)
                {
                    try
                    {
                        // Apply soft limits (values are already in pulses from configuration)
                        int result = _motorController._motionCard.GA_SetSoftLimit(_axisNumber,
                            (int)config.Axes[0].SoftLimitMax,
                            (int)config.Axes[0].SoftLimitMin);

                        if (result != 0)
                        {
                            // Log or handle soft limit setup error
                            Console.WriteLine($"Failed to set soft limits. Error code: {result}");
                        }

                        // Apply hard limit settings
                        if (config.Axes[0].IsHardLimitEnabled)
                        {
                            // Enable hard limits
                            // Using origin signal as hard limit (second parameter = 0)
                            // Using main card (third parameter = 0)
                            // IO index 7 (fourth parameter = 7)
                            result = _motorController._motionCard.GA_SetHardLimP(_axisNumber, 0, 0, 7);

                            if (result != 0)
                            {
                                Console.WriteLine($"Failed to enable hard limits. Error code: {result}");
                            }
                        }
                        else
                        {
                            // Disable hard limits by setting soft limits to maximum range
                            // This effectively disables hard limit functionality
                            result = _motorController._motionCard.GA_SetSoftLimit(_axisNumber, 2147483647, -2147483648);

                            if (result != 0)
                            {
                                Console.WriteLine($"Failed to disable hard limits. Error code: {result}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // Handle any exceptions during configuration application
                        Console.WriteLine($"Error applying configuration: {ex.Message}");
                        // Optionally log to file or show user notification
                    }
                }
            }
            return;
        }

        private void InitializePlot()
        {
            FrictionPlotModel = new PlotModel { Title = LocalizationHelper.GetLocalizedString("FrictionDisplacementChart") };
            _frictionSeries = new LineSeries { Title = LocalizationHelper.GetLocalizedString("Friction"), MarkerType = MarkerType.Circle };
            FrictionPlotModel.Series.Add(_frictionSeries);
        }

        private void UpdateFrictionChart(SensorData data)
        {
            //Dispatcher.Invoke(() =>
            //{
            //    _frictionSeries.Points.Add(new DataPoint(data.Position, data.Friction));
            //    FrictionPlotModel.InvalidatePlot(true);
            //});
            Application.Current.Dispatcher.Invoke(() =>
            {
                _frictionSeries.Points.Add(new DataPoint(data.Position, data.SensorValue));
                FrictionPlotModel.InvalidatePlot(true);
            }, System.Windows.Threading.DispatcherPriority.Background);
        }

        private void EmergencyStopButtonFriction_Click(object sender, RoutedEventArgs e)
        {
            _testController.StopTest();
        }

        private void FrictionTestButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("此为当前界面", "温馨提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }


        private void Button_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_settingWindow == null)
                {
                    _settingWindow = new SettingWindow();
                    _settingWindow.Closed += (s, args) =>
                    {
                        _settingWindow = null;
                        SettingButton.IsEnabled = true;
                    };
                    SettingButton.IsEnabled = false;
                    _settingWindow.Show();
                }
                else
                {
                    _settingWindow.Activate();
                    Logger.Log("Attempted to reopen an already open setting window.");
                }
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
            }

        }

        private void SpeedTestButton_Click(object sender, RoutedEventArgs e)
        {
            //MessageBox.Show("该模块正在开发中", "温馨提示", MessageBoxButton.OK, MessageBoxImage.Information);
            SpeedTestWindow speedTestWindow = new SpeedTestWindow(_motorController);
            speedTestWindow.ShowDialog();

        }

        private async void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isTestRunning) { return; }
            _isTestRunning = true;
            ResetButton.IsEnabled = false;

            try
            {
                if (DataContext is MainViewModel viewModel)
                {

                    await _testController.StopTestAsync();
                    viewModel.Reset();
                    await _testController.ResetPosition();
                }
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
                MessageBox.Show($"回零过程中出错：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _isTestRunning = false;
                ResetButton.Dispatcher.Invoke(() => ResetButton.IsEnabled = true);
            }

        }

        private async void StartAutoButtonFriction_Click(object sender, RoutedEventArgs e)
        {
            if (_isTestRunning) return; // Exit if already running
            _isTestRunning = true;
            StartAutoButtonFriction.IsEnabled = false;

            GuideModel = GuideModelInput.Text;
            Preload = PreloadInput.Text;
            SealLubrication = SealLubricationInput.Text;

            GuideModelInput.Text = GuideModel;
            PreloadInput.Text = Preload;
            SealLubricationInput.Text = SealLubrication;

            string workpieceName = $"{GuideModel}{Preload}{SealLubrication}";
            try
            {
                if (double.TryParse(SpeedInputFriction.Text, out double speed) &&
                    double.TryParse(AccelerationInputFriction.Text, out double acceleration) &&
                    double.TryParse(StartPositionInput.Text, out double x1) &&
                    double.TryParse(EndPositionInput.Text, out double x2))
                {
                    Logger.Log($"Starting test with speed={speed}, acceleration={acceleration}, x1={x1}, x2={x2}");
                    _frictionSeries.Points.Clear();
                    FrictionPlotModel.InvalidatePlot(true);

                    await _testController.StartAutomaticTest(speed, acceleration, x1, x2, workpieceName);

                    Logger.Log($"Test completed. Test name: {workpieceName}");
                }
                else
                {
                    MessageBox.Show("请输入有效的参数！", "输入错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
                MessageBox.Show($"Unexpected error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _isTestRunning = false;
                StartAutoButtonFriction.Dispatcher.Invoke(() => StartAutoButtonFriction.IsEnabled = true);
            }


            //// Monitor the test completion
            //await Task.Run(() =>
            //{
            //    while ((_motorController.IsMovementDone()) == false)
            //    {
            //        Task.Delay(300).Wait(); // Poll every 300 ms
            //    }
            //});
            //StartAutoButtonFriction.Dispatcher.Invoke(() => StartAutoButtonFriction.IsEnabled = true);
        }

        private void OpenDatabaseButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_databaseWindow == null)
                {
                    _databaseWindow = new DatabaseWindow();
                    _databaseWindow.Closed += (s, args) =>
                    {
                        _databaseWindow = null;
                        OpenDatabaseButton.IsEnabled = true;
                    };
                    OpenDatabaseButton.IsEnabled = false;
                    _databaseWindow.Show();
                }
                else
                {
                    _databaseWindow.Activate();
                    Logger.Log("Attempted to reopen an already open database window.");
                }
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
            }

        }

        private void LogActiveSerialPorts()
        {
            try
            {
                string[] ports = System.IO.Ports.SerialPort.GetPortNames();
                if (ports.Length > 0)
                {
                    Logger.Log("Available Serial Ports: " + string.Join(", ", ports));
                }
                else
                {
                    Logger.Log("No serial ports found.");
                }
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
            }
        }
        private void LogConnectedIPs()
        {
            try
            {
                var host = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    {
                        Logger.Log($"Connected IP Address: {ip}");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
            }
        }


        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            short status = 0;
            int iRes = _motorController._motionCard.GA_HomeGetSts(1, ref status);
            MessageBox.Show("回零状态：", status.ToString());


        }

        private void ToggleHandWheelMode(bool enabled)
        {
            if (enabled)
            {
                _motorController.StartHandwheelMode(_axisNumber); // Assuming axis 1 for handwheel
            }
            else
            {
                _motorController.EndHandwheelMode(_axisNumber);
            }
        }

        private void ToggleJoystickMode(bool enabled)
        {
            if (enabled)
            {
                _motorController.StartJoystickMode(_axisNumber);
            }
            else
            {
                _motorController.EndJoystickMode(_axisNumber);
            }
        }

        private void HandWheelButton_Click(object sender, RoutedEventArgs e)
        {
            _isJoystickOn = !_isJoystickOn; // Toggle state

            if (_isJoystickOn)
            {
                ToggleJoystickMode(true); // Enable Joystick mode
                HandwheelButton.Content = LocalizationHelper.GetLocalizedString("JoystickOn"); // Update button text
                Logger.Log("Joystick mode enabled.");
            }
            else
            {
                ToggleJoystickMode(false); // Disable Joystick mode
                HandwheelButton.Content = LocalizationHelper.GetLocalizedString("JoystickOff"); // Update button text
                Logger.Log("Joystick mode disabled.");
            }
        }

        private void ExitHandWheelMode_Click(object sender, RoutedEventArgs e)
        {
            //ToggleHandWheelMode(false);
            ToggleJoystickMode(false); // Disable Joystick mode
        }


        private void JogP_MouseDown(object sender, RoutedEventArgs e)
        {
            _motorController.HandleExternalInput(1);
            short nAxisNum = 1;
            int iRes = 0;
            double vel = 100;
            MultiCardCS.MultiCardCS.TJogPrm m_JogPrm;

            //加速度，单位：脉冲/毫秒/毫秒
            m_JogPrm.dAcc = 1;
            //减速度，单位：脉冲/毫秒/毫秒
            m_JogPrm.dDec = 1;
            //平滑时间(需要设置为0)
            m_JogPrm.dSmooth = 0;

            //使能轴（通常设置一次即可，不是每次必须）
            iRes = _motorController._motionCard.GA_AxisOn(nAxisNum);
            //设置为速度模式（通常设置一次即可，不是每次必须）
            iRes = _motorController._motionCard.GA_PrfJog(nAxisNum);

            //设置运动参数
            iRes = _motorController._motionCard.GA_SetJogPrm(nAxisNum, ref m_JogPrm);

            //设置速度
            iRes = _motorController._motionCard.GA_SetVel(nAxisNum, vel);

            //启动运动
            iRes = _motorController._motionCard.GA_Update(0X0001 << (nAxisNum - 1));
        }

        private void JogP_MouseUp(object sender, RoutedEventArgs e)
        {
            _motorController.HandleExternalInput(2);
            //停止运动
            _motorController._motionCard.GA_Stop(0XFFFFF, 0XFFFFF);
        }

        private void JogN_MouseDown(object sender, RoutedEventArgs e)
        {
            _motorController.HandleExternalInput(1);
            short nAxisNum = 1;
            int iRes = 0;
            double vel = -100;
            MultiCardCS.MultiCardCS.TJogPrm m_JogPrm;

            //加速度，单位：脉冲/毫秒/毫秒
            m_JogPrm.dAcc = 1;
            //减速度，单位：脉冲/毫秒/毫秒
            m_JogPrm.dDec = 1;
            //平滑时间(需要设置为0)
            m_JogPrm.dSmooth = 0;

            //使能轴（通常设置一次即可，不是每次必须）
            iRes = _motorController._motionCard.GA_AxisOn(nAxisNum);
            //设置为速度模式（通常设置一次即可，不是每次必须）
            iRes = _motorController._motionCard.GA_PrfJog(nAxisNum);

            //设置运动参数
            iRes = _motorController._motionCard.GA_SetJogPrm(nAxisNum, ref m_JogPrm);

            //设置速度
            iRes = _motorController._motionCard.GA_SetVel(nAxisNum, vel);

            //启动运动
            iRes = _motorController._motionCard.GA_Update(0X0001 << (nAxisNum - 1));
        }

        private void JogN_MouseUp(object sender, RoutedEventArgs e)
        {
            _motorController.HandleExternalInput(2);
            //停止运动
            _motorController._motionCard.GA_Stop(0XFFFFF, 0XFFFFF);
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            _motorController.SetOrigin(_axisNumber);
            MessageBox.Show(LocalizationHelper.GetLocalizedString("SetOrigin"), "Info", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void EndPositionInput_TextChanged(object sender, TextChangedEventArgs e)
        {

        }

        private void Button_Click_3(object sender, RoutedEventArgs e)
        {

        }

        private void PrintResultButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new VistaFolderBrowserDialog
            {
                Description = "Select a directory to save the plot"
            };

            // Show the dialog. It returns true if a folder was selected.
            if (dialog.ShowDialog() == true)
            {
                // Build a file path using the selected folder and the test name as the file name.
                string fileName = _testController.TestResult.TestName + ".png";
                string fullPath = System.IO.Path.Combine(dialog.SelectedPath, fileName);

                FrictionPlotModel.Background = OxyColors.White;
                // Export the plot model to PNG using OxyPlot's exporter.
                var exporter = new OxyPlot.Wpf.PngExporter
                {
                    Width = 800,
                    Height = 600,
                    //Background = OxyPlot.OxyColors.White
                };

                exporter.ExportToFile(FrictionPlotModel, fullPath);
                System.Windows.MessageBox.Show($"Plot saved to:\n{fullPath}", "Export Successful", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void ResetEstop_Click(object sender, RoutedEventArgs e)
        {
            _motorController.ResetEStop();
            // re-enable jog buttons (or other UI)
            MessageBox.Show("E-stop cleared. You may now resume motion.", "E-stop Reset", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void JogP_Click(object sender, RoutedEventArgs e)
        {

        }

        private void JogN_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}
