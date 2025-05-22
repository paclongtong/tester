using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO.Ports;
using System.Windows;

namespace friction_tester
{
    public class DataAcquisition
    {
        private List<SensorData> _dataBuffer = new List<SensorData>();
        private SerialPort _serialPort;
        private readonly IMotionController _motionController;
        private bool _isSimulationMode;
        public double PrecisionThreshold = 0.01;
        public double _lastRecordedPosition = 0;
        public DataAcquisition(bool isSimulatedMode, IMotionController motionController)
        {
            _isSimulationMode = isSimulatedMode;
            _motionController = motionController;
            if (!_isSimulationMode)
            {
                InitializeSerialPort();
            }
        }

        private void InitializeSerialPort()
        {
            _serialPort = new SerialPort
            {
                PortName = "COM1",
                BaudRate = 9600,
                Parity = Parity.None,
                DataBits = 8,
                StopBits = StopBits.One,
                Handshake = Handshake.None
            };
            string[] ports = SerialPort.GetPortNames();
            if (ports.Length > 0)
                _serialPort.PortName = ports[0]; // pick the first available
            else
            {
                Logger.Log("没有找到串口设备，请检查连接。");
                throw new Exception("No serial ports found.");
            }


            _serialPort.DataReceived += OnDataReceived;
            _serialPort.Open();
        }

        private void OnDataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                string data = _serialPort.ReadLine();
                string[] values = data.Split(',');
                if (values.Length == 1 &&
                    double.TryParse(values[0], out double friction))
                {
                    double position = _motionController.GetCurrentPosition();
                    if (Math.Abs(position - _lastRecordedPosition) > PrecisionThreshold)
                    {
                        var sensorData = new SensorData
                        {
                            Position = position,
                            //Friction = friction,
                            SensorValue = friction,
                            Timestamp = DateTime.Now
                        };
                        lock (_dataBuffer)
                        {
                            _dataBuffer.Add(sensorData);
                        }

                        OnDataCollected?.Invoke(sensorData);
                        _lastRecordedPosition = position;
                    }

                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"解析传感器数据时出错，错误信息: {ex.Message}");
                MessageBox.Show($"解析传感器数据时出错，错误信息: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                Logger.LogException(ex);
            }
        }
        public event Action<SensorData> OnDataCollected;
        public SensorData CollectDataAtPosition(double position)
        {
            if (_isSimulationMode)
            {
                var data = new SensorData
                {
                    Position = position,
                    SensorValue = new Random().NextDouble(), // Simulated friction
                    Timestamp = DateTime.Now
                };

                _dataBuffer.Add(data);
                return data;
            }
            else
            {
                lock (_dataBuffer)
                {
                    return _dataBuffer.LastOrDefault(d => Math.Abs(d.Position - position) < 0.01);
                }
            }
        }
        public List<SensorData> GetBuffer()
        {
            lock (_dataBuffer)
            {
                return new List<SensorData>(_dataBuffer); // Return a copy to ensure thread safety
            }
        }

        public void ClearBuffer()
        {
            lock (_dataBuffer)
            {
                _dataBuffer.Clear();
            }
        }


        public void Close()
        {
            if (!_isSimulationMode && _serialPort?.IsOpen == true)
            {
                _serialPort.Close();
                _serialPort.Dispose();
            }
        }
    }
}

