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
        private Timer _dataCollectionTimer;
        private readonly object _lockObject = new object();

        // DY500 Configuration
        private byte _stationAddress = 1; // Default station address
        private int _samplingRate = 40; // 40 samples/second (from manual: sampling rate 3)
        public DataAcquisition(bool isSimulatedMode, IMotionController motionController)
        {
            _isSimulationMode = isSimulatedMode;
            _motionController = motionController;
            if (!_isSimulationMode)
            {
                InitializeSerialPort();
                InitializeDY500();
            }
        }

        private void InitializeSerialPort()
        {
            _serialPort = new SerialPort
            {
                PortName = "COM2",
                BaudRate = 19200,
                Parity = Parity.None,
                DataBits = 8,
                StopBits = StopBits.One,
                Handshake = Handshake.None,
                ReadTimeout = 1000,
                WriteTimeout = 1000
            };
            //string[] ports = SerialPort.GetPortNames();
            //if (ports.Length > 0)
            //    _serialPort.PortName = ports[0]; // pick the first available
            //else
            //{
            //    Logger.Log("没有找到串口设备，请检查连接。Check connection, no serial port found");
            //    throw new Exception("No serial ports found.");
            //}

            try
            {
                _serialPort.Open();
                Logger.Log($"Serial port {_serialPort.PortName} opened successfully");
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
                //throw new Exception($"Failed to open serial port: {ex.Message}");
            }
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
                MessageBox.Show($"解析传感器数据时出错，错误信息: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                Logger.LogException(ex);
            }
        }
        public event Action<SensorData> OnDataCollected;

        private void InitializeDY500()
        {
            try
            {
                // Configure DY500 for optimal data collection
                // Set sampling rate to maximum (40 S/s)
                WriteModbusRegister(0x9C6A, 3); // Sampling Rate = 3 (40 S/s)

                // Set communication mode to Modbus RTU
                WriteModbusRegister(0x9C6E, 1); // Comm. Mode = 1 (ModbusRTU)

                // Set stability settings for responsive data collection
                WriteModbusRegister(0x9C58, 50); // Stability Period = 50ms (faster response)
                WriteModbusRegister(0x9C56, 2);  // Stability Range = 2 (more sensitive)

                // Set digital filter for balance between stability and responsiveness
                WriteModbusRegister(0x9C5C, 5); // Digital Filter = 5 (faster response)

                Logger.Log("DY500 sensor configured successfully");
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
                Logger.Log("Warning: Failed to configure DY500, using default settings");
            }
        }

        public async Task<SensorData> CollectDataAtPositionAsync(double position)
        {
            if (_isSimulationMode)
            {
                var data = new SensorData
                {
                    Position = position,
                    SensorValue = Math.Sin(position * 0.1) + new Random().NextDouble() * 0.1, // More realistic simulation
                    Timestamp = DateTime.Now
                };

                lock (_lockObject)
                {
                    _dataBuffer.Add(data);
                }
                return data;
            }
            else
            {
                try
                {
                    // Read current weight/force value from DY500
                    float sensorValue = await ReadWeightValueAsync();

                    var data = new SensorData
                    {
                        Position = position,
                        SensorValue = sensorValue,
                        Timestamp = DateTime.Now
                    };

                    // Only add data if position has changed significantly
                    if (Math.Abs(position - _lastRecordedPosition) > PrecisionThreshold)
                    {
                        lock (_lockObject)
                        {
                            _dataBuffer.Add(data);
                        }
                        _lastRecordedPosition = position;
                        OnDataCollected?.Invoke(data);
                    }

                    return data;
                }
                catch (Exception ex)
                {
                    Logger.LogException(ex);
                    return null;
                }
            }
        }

        // Legacy method for backward compatibility
        public SensorData CollectDataAtPosition(double position)
        {
            return CollectDataAtPositionAsync(position).GetAwaiter().GetResult();
        }
        public SensorData CollectDataAtPositionDeprecated(double position)
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

        private async Task<float> ReadWeightValueAsync()
        {
            try
            {
                // Read measurement value (register 0x9C40) using Modbus function code 03
                byte[] response = await SendModbusCommandAsync(0x03, 0x9C40, 2);

                if (response != null && response.Length >= 7)
                {
                    // Extract float value from response (4 bytes starting at index 3)
                    byte[] valueBytes = new byte[4];
                    Array.Copy(response, 3, valueBytes, 0, 4);

                    // Convert bytes to float (assuming big-endian format)
                    if (BitConverter.IsLittleEndian)
                        Array.Reverse(valueBytes);

                    return BitConverter.ToSingle(valueBytes, 0);
                }

                throw new Exception("Invalid response from DY500");
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
                throw new Exception($"Failed to read weight value: {ex.Message}");
            }
        }

        private async Task<byte[]> SendModbusCommandAsync(byte functionCode, ushort startAddress, ushort numRegisters)
        {
            if (_serialPort == null || !_serialPort.IsOpen)
                throw new Exception("Serial port not available");

            // Build Modbus RTU command
            List<byte> command = new List<byte>
        {
            _stationAddress,
            functionCode,
            (byte)(startAddress >> 8),
            (byte)(startAddress & 0xFF),
            (byte)(numRegisters >> 8),
            (byte)(numRegisters & 0xFF)
        };

            // Calculate CRC
            ushort crc = CalculateCRC(command.ToArray());
            command.Add((byte)(crc & 0xFF));
            command.Add((byte)(crc >> 8));

            try
            {
                // Clear input buffer
                _serialPort.DiscardInBuffer();

                // Send command
                _serialPort.Write(command.ToArray(), 0, command.Count);

                // Wait for response
                await Task.Delay(50); // Give device time to respond

                // Read response
                int bytesToRead = _serialPort.BytesToRead;
                if (bytesToRead == 0)
                {
                    // Wait a bit more and try again
                    await Task.Delay(100);
                    bytesToRead = _serialPort.BytesToRead;
                }

                if (bytesToRead > 0)
                {
                    byte[] response = new byte[bytesToRead];
                    _serialPort.Read(response, 0, bytesToRead);

                    // Verify CRC
                    if (VerifyCRC(response))
                    {
                        return response;
                    }
                    else
                    {
                        throw new Exception("CRC verification failed");
                    }
                }

                throw new Exception("No response from device");
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
                throw;
            }
        }

        private void WriteModbusRegister(ushort address, ushort value)
        {
            try
            {
                // Function code 06 (Write Single Register)
                List<byte> command = new List<byte>
            {
                _stationAddress,
                0x06,
                (byte)(address >> 8),
                (byte)(address & 0xFF),
                (byte)(value >> 8),
                (byte)(value & 0xFF)
            };

                ushort crc = CalculateCRC(command.ToArray());
                command.Add((byte)(crc & 0xFF));
                command.Add((byte)(crc >> 8));

                _serialPort.Write(command.ToArray(), 0, command.Count);

                // Wait for response
                Task.Delay(100).Wait();

                // Read and verify response
                if (_serialPort.BytesToRead > 0)
                {
                    byte[] response = new byte[_serialPort.BytesToRead];
                    _serialPort.Read(response, 0, response.Length);
                    // Response verification could be added here
                }
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
                throw new Exception($"Failed to write register {address:X4}: {ex.Message}");
            }
        }

        private ushort CalculateCRC(byte[] data)
        {
            ushort crc = 0xFFFF;
            foreach (byte b in data)
            {
                crc ^= b;
                for (int i = 0; i < 8; i++)
                {
                    if ((crc & 0x0001) != 0)
                    {
                        crc >>= 1;
                        crc ^= 0xA001;
                    }
                    else
                    {
                        crc >>= 1;
                    }
                }
            }
            return crc;
        }

        private bool VerifyCRC(byte[] data)
        {
            if (data.Length < 3) return false;

            byte[] dataWithoutCRC = new byte[data.Length - 2];
            Array.Copy(data, 0, dataWithoutCRC, 0, dataWithoutCRC.Length);

            ushort calculatedCRC = CalculateCRC(dataWithoutCRC);
            ushort receivedCRC = (ushort)(data[data.Length - 2] | (data[data.Length - 1] << 8));

            return calculatedCRC == receivedCRC;
        }

        public async Task StartContinuousDataCollectionAsync()
        {
            if (_isSimulationMode) return;

            _dataCollectionTimer = new Timer(async _ =>
            {
                try
                {
                    double currentPosition = _motionController.GetCurrentPosition();
                    await CollectDataAtPositionAsync(currentPosition);
                }
                catch (Exception ex)
                {
                    Logger.LogException(ex);
                }
            }, null, 0, 1000 / _samplingRate); // Collect at specified sampling rate
        }

        public void StopContinuousDataCollection()
        {
            _dataCollectionTimer?.Dispose();
            _dataCollectionTimer = null;
        }

        // Configuration methods
        public void SetStationAddress(byte address)
        {
            _stationAddress = address;
        }

        public void SetSamplingRate(int rate)
        {
            _samplingRate = Math.Max(1, Math.Min(40, rate)); // Limit to valid range
        }

        public void SetBaudRate(int baudRate)
        {
            if (_serialPort != null && !_serialPort.IsOpen)
            {
                _serialPort.BaudRate = baudRate;
            }
        }
    }
}

