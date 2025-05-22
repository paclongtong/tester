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

        // Pass the motion controller instance from the main window
        public SpeedTestWindow(IMotionController motionController)
        {
            InitializeComponent();
            _motionController = motionController;
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
            for (int i = 0; i < repetitions; i++)
            {
                // Move to the start position.
                _motionController.MoveToPosition(startPos, maxVelocity, acceleration);
                //while (Math.Abs(_motionController.GetCurrentPosition() - startPos) > precision)
                //{
                //    await Task.Delay(10);
                //}
                while(!_motionController.IsMovementDone())
                {
                    await Task.Delay(10);
                }

                // Move to the end position.
                _motionController.MoveToPosition(endPos, maxVelocity, acceleration);
                //while (Math.Abs(_motionController.GetCurrentPosition() - endPos) > precision)
                //{
                //    await Task.Delay(10);
                //}
                while (!_motionController.IsMovementDone())
                {
                    await Task.Delay(10);
                }
            }

            Logger.Log("Speed test completed.");
            MessageBox.Show("速度测试完成 Speed test completed.", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            this.Close();
        }
    }
}

