using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Threading;
using friction_tester;

public class SimulatedMotionController : IMotionController
{
    private double _currentPosition = 0;
    private bool _isMoving = false;
    private CancellationTokenSource _cancellationTokenSource;
    public event Action OnEStopTriggered;
    public MultiCardCS.MultiCardCS _motionCard { get; set; }
    public bool IsHandwheelMode { get; set; } = false; // Default to automatic mode

    public void StartHandwheelMode(short axisNum) { IsHandwheelMode = true; }
    public void EndHandwheelMode(short axisNum) { IsHandwheelMode = false; }
    public int GetHandwheelInput() { return 0; }
    public void MoveToPosition(double position, int maxVelocity, double acceleration)
    {
        _isMoving = true;
        _cancellationTokenSource = new CancellationTokenSource();

        if (IsHandwheelMode)
        {
            Logger.Log("Attempted to move while in Handwheel mode: Handwheel mode is active.");
            return;
        }

        Task.Run(() =>
        {
            try
            {
                while (!_cancellationTokenSource.Token.IsCancellationRequested  && Math.Abs(_currentPosition - position) > 0.01)
                    {
                        _currentPosition += (_currentPosition < position) ? maxVelocity : 0;
                        Thread.Sleep(1);
                }
            }
            catch(Exception ex)
            {
                Logger.LogException(ex);
                _isMoving = false;
            }
            finally
            {
                _isMoving = false;
            }
        }, _cancellationTokenSource.Token);
    }

    public double GetCurrentPosition() => _currentPosition;

    public bool IsMovementDone() => !_isMoving;
    public void HomeAxis()
    {   
        _currentPosition = 0;
        _isMoving = false;
    }

    public void Stop()
    {
        _cancellationTokenSource?.Cancel();
        _isMoving = false;
        //throw new Exception("Stop method not implemented");
    }

    public void Initialize() { }

    public int GetStatus() { return 1; }
    public void SetOrigin(short numStation) {  }

    public void HandleExternalInput(int inputCode)
    { return; }

    public Task MoveToPositionAsync(double position, int maxVelocity, double acceleration)
    {
        MoveToPosition(position, maxVelocity, acceleration);
        return Task.CompletedTask;
    }
    public void StartJoystickMode(short axisNum)
    {
        return; // Simulated controller does not support joystick mode
    }

    public void EndJoystickMode(short axisNum)
    {
        return; // Simulated controller does not support joystick mode
    }

    public void StartEStopMonitor() { return; } // Simulated controller does not support E-Stop monitoring

    public void ResetEStop() { return; } // Simulated controller does not support E-Stop reset
}

