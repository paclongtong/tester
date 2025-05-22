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

namespace friction_tester
{

    public class RealMotionController : IMotionController
    {
        private double _currentPosition = 0;
        private bool _isMoving = false;
        private readonly short AxisNumber = 1; // Default axis for motion control
        public MultiCardCS.MultiCardCS _motionCard {  get; set; }
        public bool IsHandwheelMode { get; set; } = false; // Default to automatic mode
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
                    MessageBox.Show("打开运动控制卡失败！");
                    Logger.Log("打开运动控制卡失败");
                    //throw new Exception("打开运动控制卡失败");
                }
                else
                {
                    MessageBox.Show("打开运动控制卡成功！");
                    Logger.Log("打开运动控制卡成功");
                }
                //LogError("Failed to open motion control card.");

                //result = _motionCard.GA_Reset();
                //if (result != 0) throw new Exception("重置运动控制卡失败");

                _motionCard.GA_ECatLoadPDOConfig(0);
                Task.Delay(500); 
                result = _motionCard.GA_ECatInit();
                _motionCard.GA_ECatLoadOrgPosAbs(1);
                _motionCard.GA_AxisOn(AxisNumber);

                if (result != 0)
                {
                    Logger.Log("EtherCAT initialization failed.");
                    //throw new Exception("EtherCAT 初始化失败");
                    MessageBox.Show(GetLocalizedString("EtherCATInitFail"), GetLocalizedString("Error"), MessageBoxButton.OK);
                }
                else if(result == 1){
                    Logger.Log("EtherCAT 初始化成功");
                }

                
                //// Scan slaves
                //short nCount = 100;
                //string strText;

                //Task.Delay(500);
                //_motionCard.GA_ECatGetSlaveCount(ref nCount);

                //strText = string.Format("扫描到{0:G}个从站", nCount);
                //MessageBox.Show(strText);

                //short pCutInitSlaveNum = 0;
                //short pMode = 0; ;
                //short pModeStep = 0;
                ////string strText;

                //_motionCard.GA_ECatGetInitStep(ref pCutInitSlaveNum, ref pMode, ref pModeStep);

                //strText = string.Format("正在初始化第{0:G}个站点,当前模式{0:G}，子步{0:G}", pCutInitSlaveNum, pMode, pModeStep);
                //MessageBox.Show(strText);
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
            }

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

        public void SetOrigin(short numStation=1)
        {
            _motionCard.GA_ECatSetOrgPosCur(numStation);
        }

        public void MoveToPosition(double position, int maxVelocity, double acceleration)
        {   
            if (IsHandwheelMode) 
            {
                Logger.Log("Attempted to move while in Handwheel mode: Handwheel mode is active.");
                return;
            }
            short AxisNumber = 1;
            _isMoving = true;
            int iRes = 0;
            var trapPrm = new TTrapPrm
            {
                acc = acceleration,
                dec = acceleration,
                velStart = 0,
                smoothTime = 0  // Smooth time to be defined in configuration file
            };
            //使能轴（通常设置一次即可，不是每次必须）
            iRes = _motionCard.GA_AxisOn(AxisNumber);
            //设置为点位模式（通常设置一次即可，不是每次必须）
            int result = _motionCard.GA_PrfTrap(AxisNumber);

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
            if (result != 0)
                throw new Exception("无法启动点位运动");
            
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
            if (IsHandwheelMode)
            {
                Logger.Log("Stop command ignored in handwheel mode.");
                MessageBox.Show("手轮模式下，忽略停止按键功能");
                return;
            }

            short iAxisStatus = 0;
            //int iClock = 0;
            int result = _motionCard.GA_HomeGetSts(AxisNumber, ref iAxisStatus);
            if ( result == 1)  
            {
                result = _motionCard.GA_HomeStop(AxisNumber);
                if (result == 0)
                {
                    Logger.Log("回零过程中触发停止");
                }
            }
            else if (result == 0)
            {
                _motionCard.GA_Stop(0XFFFFF, 0XFFFFF);
                Logger.Log("常规紧急停止功能触发");
            }
            else
            {
                _motionCard.GA_Stop(0XFFFFF, 0XFFFFF);
                Logger.Log("常规紧急停止功能触发");
            }
            
            if (result != 0) throw new Exception("Failed to stop motion.");
            _isMoving = false;
        }

        // **Three-Color Light Output**    //  AXIS_STATUS_HOME_RUNNING = 0x00001000
        public void SetLightOutput(string color)
        {
            switch (color)
            {
                case "red":
                    // Example of setting the red light (customize per your hardware)
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
    }

}
