using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Threading;
using friction_tester;
using MultiCardCS;
using static MultiCardCS.MultiCardCS.TJogPrm;
using static MultiCardCS.MultiCardCS;

public class StatusPanelViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly DispatcherTimer _statusTimer;
    private readonly TestController _testController;
    private bool _isTestInProgress = false; // Flag to indicate if a test is running

    // Status properties
    private string _axisStatusText;
    private string _etherCatStatusText;
    private string _sensorValueText;
    private bool _isAxisEnabled;
    private bool _isEtherCatInitialized;
    private float _sensorValue;

    public StatusPanelViewModel(TestController testController)
    {
        _testController = testController;

        // Subscribe to TestStateManager's event
        TestStateManager.OnTestActivityChanged += HandleTestActivityChanged;

        // Initialize timer for polling status
        _statusTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500) // Poll every 500ms
        };
        _statusTimer.Tick += async (s, e) => await UpdateStatusAsync();

        // Initialize default values
        _axisStatusText = "Unknown";
        _etherCatStatusText = "Unknown";
        _sensorValueText = "0.0";
        _isAxisEnabled = false;
        _isEtherCatInitialized = false;
        _sensorValue = 0.0f;

        StartPolling();
    }

    // Properties for binding
    public string AxisStatusText
    {
        get => _axisStatusText;
        set
        {
            _axisStatusText = value;
            OnPropertyChanged();
        }
    }

    public string EtherCatStatusText
    {
        get => _etherCatStatusText;
        set
        {
            _etherCatStatusText = value;
            OnPropertyChanged();
        }
    }

    public string SensorValueText
    {
        get => _sensorValueText;
        set
        {
            _sensorValueText = value;
            OnPropertyChanged();
        }
    }

    public bool IsAxisEnabled
    {
        get => _isAxisEnabled;
        set
        {
            _isAxisEnabled = value;
            OnPropertyChanged();
        }
    }

    public bool IsEtherCatInitialized
    {
        get => _isEtherCatInitialized;
        set
        {
            _isEtherCatInitialized = value;
            OnPropertyChanged();
        }
    }

    public float SensorValue
    {
        get => _sensorValue;
        set
        {
            _sensorValue = value;
            OnPropertyChanged();
        }
    }

    public void StartPolling()
    {
        _statusTimer.Start();
    }

    public void StopPolling()
    {
        _statusTimer.Stop();
    }

    private async Task UpdateStatusAsync()
    {
        try
        {
            // Update Axis Status
            await UpdateAxisStatusAsync();

            // Update EtherCAT Status
            UpdateEtherCatStatus();

            // Update Sensor Value
            await UpdateSensorValueAsync();
        }
        catch (Exception ex)
        {
            // Log exception but don't stop polling
            Logger.Log($"Status update error: {ex.Message}");
        }
    }

    private async Task UpdateAxisStatusAsync()
    {
        try
        {
            // Get all system status
            //TAllSysStatusDataSX statusData = new TAllSysStatusDataSX();
            //int result = _testController._motorController._motionCard.GA_GetAllSysStatusSX(ref statusData);
            int axisStatus = 0;
            int iClock = 0;

            int result = _testController._motorController._motionCard.GA_GetSts(1, ref axisStatus, 1, ref iClock); // the first 1 as in axisNumber, the third 1 is a fixed value

            if (result == 0)
            {

                // Check if axis is enabled
                bool isEnabled = (axisStatus & AXIS_STATUS_ENABLE) != 0;
                IsAxisEnabled = isEnabled;

                // Determine status text based on flags
                if ((axisStatus & AXIS_STATUS_ESTOP) != 0)
                {
                    AxisStatusText = "EmergencyStop";
                }
                else if ((axisStatus & AXIS_STATUS_SV_ALARM) != 0)
                {
                    AxisStatusText = "Alarm";
                }
                else if ((axisStatus & AXIS_STATUS_RUNNING) != 0)
                {
                    AxisStatusText = "Running";
                }
                else if ((axisStatus & AXIS_STATUS_HOME_RUNNING) != 0)
                {
                    AxisStatusText = "Homing";
                }
                else if (isEnabled)
                {
                    AxisStatusText = "Enabled";
                }
                else
                {
                    AxisStatusText = "Disabled";
                }
            }
            else
            {
                AxisStatusText = "Error";
                IsAxisEnabled = false;
            }
        }
        catch (Exception ex)
        {
            AxisStatusText = "Error";
            IsAxisEnabled = false;
            System.Diagnostics.Debug.WriteLine($"Axis status update error: {ex.Message}");
        }
    }

    private void UpdateEtherCatStatus()
    {
        try
        {
            int iRes = 0;
            short nCutInitSlaveNum = 0;
            short nMode = 0;
            short nModeStep = 0;

            iRes = _testController._motorController._motionCard.GA_ECatGetInitStep(ref nCutInitSlaveNum, ref nMode, ref nModeStep);

            if (iRes == 0)
            {
                switch (nCutInitSlaveNum)
                {
                    case -1:
                        EtherCatStatusText = "NotInitialized";
                        IsEtherCatInitialized = false;
                        break;
                    case 0:
                        EtherCatStatusText = "Initialized";
                        IsEtherCatInitialized = true;
                        break;
                    default:
                        EtherCatStatusText = "Initializing";
                        IsEtherCatInitialized = false;
                        break;
                }
            }
            else
            {
                EtherCatStatusText = "Error";
                IsEtherCatInitialized = false;
            }
        }
        catch (Exception ex)
        {
            EtherCatStatusText = "Error";
            IsEtherCatInitialized = false;
            System.Diagnostics.Debug.WriteLine($"EtherCAT status update error: {ex.Message}");
        }
    }

    private async Task UpdateSensorValueAsync()
    {
        if (_isTestInProgress)
        {
            // SensorValueText = "Testing..."; // Or some other indicator
            return; // Skip polling if a test is active
        }

        try
        {
            if (_testController._dataAcquisition != null)
            {
                // Use reflection to call the private ReadWeightValueAsync method
                var method = _testController._dataAcquisition.GetType().GetMethod("ReadWeightValueAsync",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                if (method != null)
                {
                    var task = (Task<float>)method.Invoke(_testController._dataAcquisition, null);
                    float value = await task;
                    SensorValue = value;
                    SensorValueText = $"{value:F2}";
                }
                else
                {
                    SensorValueText = "N/A";
                }
            }
            else
            {
                SensorValueText = "N/A";
            }
        }
        catch (Exception ex)
        {
            SensorValueText = "Error";
            System.Diagnostics.Debug.WriteLine($"Sensor value update error: {ex.Message}");
        }
    }

    public event PropertyChangedEventHandler PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public void Dispose()
    {
        StopPolling();
        _statusTimer?.Stop();
        // Unsubscribe from TestStateManager's event
        TestStateManager.OnTestActivityChanged -= HandleTestActivityChanged;
        
        // Removed: Unsubscribe from _testController events as they are no longer used here for test state
        // if (_testController != null)
        // {
        //     _testController.OnTestStarted -= TestController_OnTestStarted;
        //     _testController.OnTestCompleted -= TestController_OnTestCompleted;
        // }
    }

    private void HandleTestActivityChanged(bool isTestActive)
    {
        _isTestInProgress = isTestActive;
        if (isTestActive)
        {
            Logger.Log("[StatusPanelViewModel] TestStateManager reported test started, sensor polling paused.");
            // SensorValueText = "Testing..."; // Optional: Update UI to indicate testing
        }
        else
        {
            Logger.Log("[StatusPanelViewModel] TestStateManager reported all tests completed, sensor polling resumed.");
        }
    }

    // Axis status constants (matching your definitions)
    private const uint AXIS_STATUS_ESTOP = 0x00000001;
    private const uint AXIS_STATUS_SV_ALARM = 0x00000002;
    private const uint AXIS_STATUS_POS_SOFT_LIMIT = 0x00000004;
    private const uint AXIS_STATUS_NEG_SOFT_LIMIT = 0x00000008;
    private const uint AXIS_STATUS_FOLLOW_ERR = 0x00000010;
    private const uint AXIS_STATUS_POS_HARD_LIMIT = 0x00000020;
    private const uint AXIS_STATUS_NEG_HARD_LIMIT = 0x00000040;
    private const uint AXIS_STATUS_IO_SMS_STOP = 0x00000080;
    private const uint AXIS_STATUS_IO_EMG_STOP = 0x00000100;
    private const uint AXIS_STATUS_ENABLE = 0x00000200;
    private const uint AXIS_STATUS_RUNNING = 0x00000400;
    private const uint AXIS_STATUS_ARRIVE = 0x00000800;
    private const uint AXIS_STATUS_HOME_RUNNING = 0x00001000;
    private const uint AXIS_STATUS_HOME_SUCESS = 0x00002000;
    private const uint AXIS_STATUS_HOME_SWITCH = 0x00004000;
    private const uint AXIS_STATUS_INDEX = 0x00008000;
    private const uint AXIS_STATUS_GEAR_START = 0x00010000;
    private const uint AXIS_STATUS_GEAR_FINISH = 0x00020000;
}
