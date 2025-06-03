using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MultiCardCLR;
using MultiCardCS;
using System.IO;
using static MultiCardCS.MultiCardCS;
using System.Windows;
using System.Security.Principal;
using System.Threading;

namespace friction_tester
{

    public class RealMotionController : IMotionController
    {
        private double _currentPosition = 0;
        private bool _isMoving = false;
        public readonly short AxisNumber = 1; // Default axis for motion control
        public MultiCardCS.MultiCardCS _motionCard { get; set; }
        public bool IsHandwheelMode { get; set; } = false; // Default to automatic 
        public bool IsJoystickMode { get; set; } = false; // Default to automatic mode
        private Timer joystickTimer;
        private short currentAxisNum;
        private bool isJogging = false;
        private System.Timers.Timer _estopTimer;
        public event Action OnEStopTriggered;
        private CancellationTokenSource _moveCancellationTokenSource;
        
        public void Initialize()
        {
            int iRes = 0;
            int result = 0;
            _motionCard = new MultiCardCS.MultiCardCS();
            try
            {
                _motionCard.GA_StartDebugLog(0);
                iRes = _motionCard.GA_Open(1, "192.168.0.200", 60000, "192.168.0.1", 60000);
                //iRes = _motionCard.GA_Open(0, "192.168.1.221");
                // Open the board and reset it
                //int result = _motionCard._motionCard.GA_Open(0, "192.168.1.221"); // Network mode, example IP
                if (iRes != 0)
                {
                    MessageBox.Show(LocalizationHelper.GetLocalizedString("EnableMotionCardFailure"));
                    Logger.Log("Failed to intialize motion control card.");
                }
                else
                {
                    MessageBox.Show(LocalizationHelper.GetLocalizedString("EnableMotionCardSuccess"));
                    Logger.Log("Motion controll card enabled");
                }
                //LogError("Failed to open motion control card.");

                _motionCard.GA_ECatLoadPDOConfig(0);
                Task.Delay(500);
                result = _motionCard.GA_ECatInit();
                _motionCard.GA_ECatLoadOrgPosAbs(1);
                Thread.Sleep(5000);
                _motionCard.GA_AxisOn(AxisNumber);

                if (result != 0)
                {
                    Logger.Log("EtherCAT initialization failed.");
                    //throw new Exception("EtherCAT failed");
                    MessageBox.Show(GetLocalizedString("EtherCATInitFail"), GetLocalizedString("Error"), MessageBoxButton.OK);
                }
                else if (result == 1) {
                    Logger.Log("EtherCAT initialization succeeded");
                }

                // 1a) Tell the motion card which I/O bit is your E-stop (args: cardNo, port, bitMask, debounce_ms)
                iRes = _motionCard.GA_EStopSetIO(0, 0, 1, 20);      // (short nCardIndex,short nIOIndex,short nEStopSns = 1 (reversed logic for NC switch),unsigned long lFilterTime)
                ApiResultHandler.HandleResult(iRes);

                // 1b) Enable E-stop monitoring on the card
                iRes = _motionCard.GA_EStopOnOff(1);
                ApiResultHandler.HandleResult(iRes);

                // 1c) Start your E-stop polling loop
                StartEStopMonitor();
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
            }

        }

        public void StartEStopMonitor()
        {
            _estopTimer = new System.Timers.Timer(50);   // poll every 50 ms
            _estopTimer.Elapsed += (s, e) =>
            {
                // 1) Query the latched E-stop status
                short latched = 0;
                int code = _motionCard.GA_EStopGetSts(ref latched);
                Logger.Log($"[EStop Poll] GA_EStopGetSts returned code={code}, latched={latched}");

                // 2) (optional) also read the **raw** input bit so you can see the NC switch
                //short raw = 0;
                //int rc2 = _motionCard.GA_GetExtDiBit(0, 0, ref raw);  // card=0, port=0, bit=0
                //Logger.Log($"[EStop Poll] GA_GetExtDiBit returned code={rc2}, rawIn={raw}");
                //ApiResultHandler.HandleResult(r);
                if (latched == 1)
                {
                    // hardware E-stop has gone active
                    _estopTimer.Stop();      // stop further polling until cleared
                    EStop();                  // your existing emergency‐stop routine :contentReference[oaicite:1]{index=1}
                    OnEStopTriggered?.Invoke();
                }
            };
            _estopTimer.Start();
        }

        public void ResetEStop()
        {
            // Only call this once the physical E-stop button is released 
            int iRes = _motionCard.GA_EStopClrSts();
            Logger.Log($"GA_EStopClrSts() return: {iRes}");
            //ApiResultHandler.HandleResult(iRes);
            // restart polling so you can detect the next trip
            StartEStopMonitor();
        }

        public static string GetLocalizedString(string key)
        {
            if (Application.Current.Resources.Contains(key))
            {
                return Application.Current.Resources[key] as string;
            }
            return key; // Fallback: return key if not found
        }

        public void StartHandwheelMode(short axisNum)
        {
            if (!IsHandwheelMode)
            {
                int result = _motionCard.GA_StartHandwheel(axisNum, 10000, 1, 1, 0, 0.1, 0.1, 50, 0);
                if (result == 0)
                {
                    IsHandwheelMode = true;
                    Logger.Log($"Handwheel mode started for Axis {axisNum}.");
                }
                else
                {
                    Logger.Log($"Failed to start handwheel mode: Error {result}");
                }
            }
        }

        public void EndHandwheelMode(short axisNum)
        {
            if (IsHandwheelMode)
            {
                int result = _motionCard.GA_EndHandwheel(axisNum);
                if (result == 0)
                {
                    IsHandwheelMode = false;
                    Logger.Log($"Handwheel mode ended for Axis {axisNum}.");
                }
                else
                {
                    Logger.Log($"Failed to end handwheel mode: Error {result}");
                }
            }
        }
        public int GetHandwheelInput()
        {
            int pValue = 0;
            int result = _motionCard.GA_GetDiRaw(7, ref pValue);
            if (result == 0)
            {
                Logger.Log($"Handwheel input value: {pValue}");
                return pValue;
            }
            Logger.Log($"Failed to get handwheel input: Error {result}");
            return -1;
        }

        public void SetOrigin(short numStation = 1)
        {
            _motionCard.GA_ECatSetOrgPosCur(numStation);
        }

        public async Task MoveToPositionAsync(double position, int maxVelocity, double acceleration, CancellationToken cancellationToken = default)
        {
            if (IsHandwheelMode || IsJoystickMode || isJogging)
            {
                Logger.Log("Attempted to move while in Handwheel mode: Manual mode is active.");
                return;
            }
            short AxisNumber = 1;
            _isMoving = true;
            int iRes = 0;
            acceleration = acceleration / 1000; // Convert from pulse/ms^2 to mm/s^2
            var trapPrm = new TTrapPrm
            {
                acc = acceleration,
                dec = acceleration,
                velStart = 0,
                smoothTime = 0  // Smooth time to be defined in configuration file
            };
            _moveCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            try
            {
                //使能轴（通常设置一次即可，不是每次必须）
                iRes = _motionCard.GA_AxisOn(AxisNumber);
                Logger.Log($"MoveToPosition: GA_AxisOn returned {iRes}");
                //设置为点位模式（通常设置一次即可，不是每次必须）
                int result = _motionCard.GA_PrfTrap(AxisNumber);
                Logger.Log($"MoveToPosition: GA_PrfTrap returned {result}");

                if (result != 0)
                {
                    MessageBox.Show("设置点位运动模式失败");
                    throw new Exception("设置指定轴为点位模式失败");
                }
                result = _motionCard.GA_SetTrapPrm(AxisNumber, ref trapPrm);
                if (result != 0)
                    throw new Exception("梯形运动参数设置出错");
                result = _motionCard.GA_SetPos(AxisNumber, (int)position);
                if (result != 0)
                    throw new Exception("目标位置设置出错");
                result = _motionCard.GA_SetVel(AxisNumber, (int)maxVelocity);
                if (result != 0)
                    throw new Exception("设置最高速度出错");
                result = _motionCard.GA_Update(1 << (AxisNumber - 1));
                Logger.Log($"MoveToPosition: GA_Update returned {result}");
                if (result != 0)
                    throw new Exception("无法启动点位运动");

                // Small delay to ensure motion has started
                await Task.Delay(50, _moveCancellationTokenSource.Token);

                // Wait for movement to complete
                while (!IsMovementDone())
                {
                    if (_moveCancellationTokenSource.Token.IsCancellationRequested)
                    {
                        Logger.Log("MoveToPositionAsync cancelled by token.");
                        break;
                    }
                    await Task.Delay(10, _moveCancellationTokenSource.Token);
                }
            }
            catch (OperationCanceledException)
            {
                Logger.Log("MoveToPositionAsync operation cancelled.");
            }
            finally
            {
                _isMoving = false;
                _moveCancellationTokenSource?.Dispose();
                _moveCancellationTokenSource = null;
            }
        }

        public void MoveToPosition(double position, int maxVelocity, double acceleration)
        {
            // Deprecated : Use MoveToPositionAsync instead
            if (IsHandwheelMode || isJogging || IsJoystickMode)
            {
                Logger.Log("Attempted to move while in manual mode: manual mode is active.");
                return;
            }
            short AxisNumber = 1;
            _isMoving = true;
            int iRes = 0;
            acceleration = acceleration / 1000; // Convert from pulse/ms^2 to mm/s^2
            var trapPrm = new TTrapPrm
            {
                acc = acceleration,
                dec = acceleration,
                velStart = 0,
                smoothTime = 0  // Smooth time to be defined in configuration file
            };
            try
            {
                //使能轴（通常设置一次即可，不是每次必须）
                iRes = _motionCard.GA_AxisOn(AxisNumber);
                //设置为点位模式（通常设置一次即可，不是每次必须）
                int result = _motionCard.GA_PrfTrap(AxisNumber);
                if (result != 0)
                    throw new Exception("设置指定轴为点位模式失败");
                result = _motionCard.GA_SetTrapPrm(AxisNumber, ref trapPrm);
                if (result != 0)
                    throw new Exception("梯形运动参数设置出错");
                result = _motionCard.GA_SetPos(AxisNumber, (int)position);
                if (result != 0)
                    throw new Exception("目标位置设置出错");
                result = _motionCard.GA_SetVel(AxisNumber, (int)maxVelocity);
                if (result != 0)
                    throw new Exception("设置最高速度出错");
                result = _motionCard.GA_Update(1 << (AxisNumber - 1));
                if (result != 0)
                    throw new Exception("无法启动点位运动");
            }
            finally
            {
                _isMoving = false;
            }
        }
        public double GetCurrentPosition()
        {
            // Query the encoder position or planned position
            double position = 0;
            int clock = 0;
            int result = _motionCard.GA_GetPrfPos(AxisNumber, ref position, 1, ref clock);
            try
            {
                if (result != 0)
                    throw new Exception("Failed to retrieve the current position. 获取当前位置失败 ");
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
            }

            return position;
        }

        public bool IsMovementDone()
        {
            //return _isMoving;
            try
            {
                int status = 0;
                int pClock = 0;
                int result = 0;
                result = _motionCard.GA_GetSts(AxisNumber, ref status, 1, ref pClock); // 3: axis 1, 4: pClock - read controller clock, null by default
                if (result == 1)
                {
                    Logger.Log($"Axis info failed to fetch, GA_GetSts failed with error code: {result}");
                    return false;
                }

                return (status & AXIS_STATUS_ARRIVE) != 0;
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
                return false;
            }
        }

        public void Stop()
        {
            if (IsHandwheelMode || isJogging || IsJoystickMode)
            {
                Logger.Log("Stop command ignored in handwheel mode.");
                MessageBox.Show("Manual mode 手动模式下，忽略停止按键功能");
                return;
            }
            int result = _motionCard.GA_Stop(0XFFFFF, 0XFFFFF);
            Logger.Log("常规停止功能触发");
            if (result != 0) throw new Exception("Failed to stop motion.");
            _isMoving = false;
            CancelMove();
        }

        public void EStop()
        {
            int result = _motionCard.GA_Stop(0XFFFFF, 0XFFFFF);
            int result_axisoff = _motionCard.GA_AxisOff(AxisNumber);
            Logger.Log("Emergency stop function executed.");
            if (result != 0) throw new Exception("Failed to Estop motion.");
            _isMoving = false;
            CancelMove();
        }

        // **Three-Color Light Output**    //  AXIS_STATUS_HOME_RUNNING = 0x00001000
        public void SetLightOutput(string color)
        {
            switch (color)
            {
                case "red":
                    _motionCard.GA_SetExtDoBit(0, 0, 1); // Red light on
                    break;
                case "yellow":
                    _motionCard.GA_SetExtDoBit(0, 1, 1); // Yellow light on
                    break;
                case "green":
                    _motionCard.GA_SetExtDoBit(0, 2, 1); // Green light on
                    break;
                default:
                    // Turn off all lights (if needed)
                    _motionCard.GA_SetExtDoBit(0, 0, 0); // Red light off
                    _motionCard.GA_SetExtDoBit(0, 1, 0); // Yellow light off
                    _motionCard.GA_SetExtDoBit(0, 2, 0); // Green light off
                    break;
            }
            Logger.Log($"Set light to {color}.");
        }

        // Helper methods to move the axis
        private void MoveAxis(int direction)
        {
            // Add your axis movement logic here (e.g., using GA_SetPos or similar methods)
            Logger.Log($"Moving axis {AxisNumber} in direction: {direction}");
        }

        public void HomeAxis()
        {
            var homeParams = new MultiCardCS.MultiCardCS.TAxisHomePrm
            {
                nHomeMode = 1, // Home mode, typically specified in the manual
                nHomeDir = -1,  // Direction: 1 for positive, -1 for negative
                dHomeRapidVel = 30.0, //回零快移速度，单位：Pluse/ms
                dHomeLocatVel = 20.0,  //回零定位速度，单位：Pluse/ms
                dHomeAcc = 10.0,
                lOffset = 0
            };

            
            try
            {
                int result = _motionCard.GA_HomeSetPrm(AxisNumber, ref homeParams);
                if (result != 0)
                {
                    MessageBox.Show("设置回原点参数失败");
                    throw new Exception("Failed to set homing parameters.");
                }
                
                result = _motionCard.GA_HomeStart(AxisNumber);
                if (result != 0)
                {
                    MessageBox.Show("启动回原点失败");
                    throw new Exception("启动回原点失败"); 
                }
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
            }
        }

        // **External Input Handling**
        public void HandleExternalInput(int inputCode)
        {
            switch (inputCode)
            {
                case 0: // Emergency stop
                    //Stop(); // Emergency stop
                    SetLightOutput("red"); // Red light for stop
                    break;
                case 1: // X1 movement (negative direction)
                    //MoveAxis(-1); // Negative direction movement
                    SetLightOutput("yellow"); // Yellow light for movement
                    break;
                case 2: // X2 movement (positive direction)
                    //MoveAxis(1); // Positive direction movement
                    SetLightOutput("green"); // Green light for successful movement
                    break;
            }
        }

        public IMotionController GetController()
        {
            return this;
        }

        public int GetStatus()
        {
            int iRes = 0;
            MultiCardCS.MultiCardCS.TAllSysStatusDataSX m_AllSysStatusData;

            m_AllSysStatusData.lAxisEncPos = new int[16];
            m_AllSysStatusData.lAxisPrfPos = new int[16];
            m_AllSysStatusData.lAxisStatus = new int[16];

            m_AllSysStatusData.nADCValue = new short[2];
            m_AllSysStatusData.lUserSegNum = new int[2];
            m_AllSysStatusData.lRemainderSegNum = new short[2];
            m_AllSysStatusData.nCrdRunStatus = new short[2];
            m_AllSysStatusData.lCrdSpace = new short[2];
            m_AllSysStatusData.dCrdVel = new float[2];

            m_AllSysStatusData.lCrdPos = new int[2][];
            m_AllSysStatusData.lCrdPos[0] = new int[5];
            m_AllSysStatusData.lCrdPos[1] = new int[5];

            m_AllSysStatusData.lLimitPosRaw = 0;
            m_AllSysStatusData.lLimitNegRaw = 0;
            m_AllSysStatusData.lAlarmRaw = 0;
            m_AllSysStatusData.lHomeRaw = 0;
            m_AllSysStatusData.lMPG = 0;
            m_AllSysStatusData.lGpiRaw = new int[8];
            m_AllSysStatusData.lGpoRaw = new int[8];

            m_AllSysStatusData.lMPGEncPos = 0;

            //8轴及以下控制卡用这个
            iRes = _motionCard.GA_GetAllSysStatusSX(ref m_AllSysStatusData);

            if (iRes == 0)
            {
                // 更新当前选中轴的规划位置
                int selectedAxisIndex = 1;
                MessageBox.Show(m_AllSysStatusData.ToString());
            }
            else
            {
                MessageBox.Show($"读取数据失败，错误码：{iRes}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            return iRes;
        }

        public void StartJoystickMode(short axisNum)
        {
            if (!IsJoystickMode)
            {
                currentAxisNum = axisNum;

                // Enable axis (set once)
                int result = _motionCard.GA_AxisOn(axisNum);
                if (result != 0)
                {
                    Logger.Log($"Failed to enable axis: Error {result}");
                    return;
                }

                // Set to jog mode (set once)
                result = _motionCard.GA_PrfJog(axisNum);
                if (result != 0)
                {
                    Logger.Log($"Failed to set jog profile: Error {result}");
                    return;
                }

                IsJoystickMode = true;

                // Start monitoring joystick inputs
                joystickTimer = new Timer(CheckJoystickInput, null, 0, 50); // Check every 50ms
                Logger.Log($"Joystick mode started for Axis {axisNum}.");
            }
        }

        public void EndJoystickMode(short axisNum)
        {
            if (IsJoystickMode)
            {
                // Stop the timer
                joystickTimer?.Dispose();
                joystickTimer = null;

                // Stop any ongoing motion
                StopJogMotion();

                IsJoystickMode = false;
                Logger.Log($"Joystick mode ended for Axis {axisNum}.");
            }
        }

        private void CheckJoystickInput(object state)
        {
            if (!IsJoystickMode) return;

            try
            {
                // Read joystick inputs
                bool jogPositive = GetJoystickInput(1); // X1 positive direction
                bool jogNegative = GetJoystickInput(2); // X2 negative direction

                if (jogPositive && !jogNegative)
                {
                    // Jog positive direction
                    if (!isJogging)
                    {
                        StartJogMotion(true); // true for positive direction
                    }
                }
                else if (jogNegative && !jogPositive)
                {
                    // Jog negative direction
                    if (!isJogging)
                    {
                        StartJogMotion(false); // false for negative direction
                    }
                }
                else
                {
                    // No input or conflicting inputs - stop motion
                    if (isJogging)
                    {
                        StopJogMotion();
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Error checking joystick input: {ex.Message}");
            }
        }

        private bool GetJoystickInput(int inputIndex)
        {
            short pValue = 0;
            int result = _motionCard.GA_GetExtDiBit(0, (short)inputIndex, ref pValue);

            if (result == 0)
            {
                return pValue == 1; // Return true if input is active
            }
            else
            {
                Logger.Log($"Failed to get joystick input {inputIndex}: Error {result}");
                return false;
            }
        }

        private void StartJogMotion(bool isPositiveDirection)
        {
            if (isJogging) return;

            try
            {
                // Set jog parameters
                MultiCardCS.MultiCardCS.TJogPrm jogPrm;
                jogPrm.dAcc = 1;    // Acceleration: pulse/millisecond/millisecond
                jogPrm.dDec = 1;    // Deceleration: pulse/millisecond/millisecond
                jogPrm.dSmooth = 0; // Smoothing time (set to 0)

                // Set motion parameters
                int result = _motionCard.GA_SetJogPrm(currentAxisNum, ref jogPrm);
                if (result != 0)
                {
                    Logger.Log($"Failed to set jog parameters: Error {result}");
                    return;
                }

                // Set speed (positive for forward, negative for reverse)
                double speed = isPositiveDirection ? 20 : -20;
                result = _motionCard.GA_SetVel(currentAxisNum, speed);
                if (result != 0)
                {
                    Logger.Log($"Failed to set jog speed: Error {result}");
                    return;
                }

                // Start motion
                result = _motionCard.GA_Update((int)(0x0001 << (currentAxisNum - 1)));
                if (result == 0)
                {
                    isJogging = true;
                    Logger.Log($"Jog motion started in {(isPositiveDirection ? "positive" : "negative")} direction");
                }
                else
                {
                    Logger.Log($"Failed to start jog motion: Error {result}");
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Exception in StartJogMotion: {ex.Message}");
            }
        }

        private void StopJogMotion()
        {
            if (!isJogging) return;

            try
            {
                // Stop the axis
                int result = _motionCard.GA_Stop((int)(0x0001 << (currentAxisNum - 1)), 0);
                if (result == 0)
                {
                    isJogging = false;
                    Logger.Log("Jog motion stopped");
                }
                else
                {
                    Logger.Log($"Failed to stop jog motion: Error {result}");
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Exception in StopJogMotion: {ex.Message}");
            }
        }

        public void CancelMove()
        {
            if (_moveCancellationTokenSource != null && !_moveCancellationTokenSource.IsCancellationRequested)
            {
                _moveCancellationTokenSource.Cancel();
            }
        }
    }

}
