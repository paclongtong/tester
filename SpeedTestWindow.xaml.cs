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
    public partial class SpeedTestWindow : Window
    {
        private readonly IMotionController _motionController;
        private readonly SpeedTestViewModel _viewModel;
        private readonly DataAcquisition _dataAcquisition;

        // Pass the motion controller instance from the main window
        public SpeedTestWindow(IMotionController motionController)
        {
            InitializeComponent();
            _motionController = motionController;
            _dataAcquisition = new DataAcquisition(true, motionController);
            _viewModel = new SpeedTestViewModel();
            DataContext = _viewModel;
        }

        private async void StartTestButton_Click(object sender, RoutedEventArgs e)
        {
            // Parse values from the input controls.
            if (!double.TryParse(StartPositionInput.Text, out double startPos) ||
                !double.TryParse(EndPositionInput.Text, out double endPos) ||
                !int.TryParse(SpeedInput.Text, out int maxVelocity) ||
                !double.TryParse(AccelerationInput.Text, out double acceleration) ||
                //!double.TryParse(PrecisionInput.Text, out double precision) ||
                !int.TryParse(RepetitionInput.Text, out int repetitions))
            {
                MessageBox.Show("请输入所有有效参数！Invalid argument(s)", "输入错误 Input error(s)", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            Logger.Log($"Speed test started with Start: {startPos}, End: {endPos}, Velocity: {maxVelocity}, " +
                       $"Acceleration: {acceleration}, Repetitions: {repetitions}");

            // Perform the back-and-forth movement for the specified number of repetitions.
            try
            {
                for (int i = 0; i < repetitions; i++)
                {
                    // Move to the start position.
                    await _motionController.MoveToPositionAsync(startPos * 1000, maxVelocity, acceleration); // 1000 pulses per mm by default
                                                                                                             // Move to the end position with data collection
                    var moveTask = _motionController.MoveToPositionAsync(endPos * 1000, maxVelocity, acceleration);

                    // Collect data during movement
                    while (!_motionController.IsMovementDone())
                    {
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

                    await moveTask; // Ensure the move task is completed

                    // Optional: Add a small delay between repetitions
                    await Task.Delay(100);
                }
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
                MessageBox.Show("速度测试过程中发生错误，请检查日志。An error occurred during the speed test, please check the logs.", "错误 Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }


            Logger.Log("Speed test completed.");
            MessageBox.Show("速度测试完成 Speed test completed.", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            this.Close();
        }
    }
}

