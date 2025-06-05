using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace friction_tester
{
    public interface IMotionController
    {
        MultiCardCS.MultiCardCS _motionCard { get; set; }
        void SetOrigin(short numStation);
        void MoveToPosition(double position, int maxVelocity, double acceleration);
        Task MoveToPositionAsync(double position, int maxVelocity, double acceleration, CancellationToken cancellationToken = default);
        double GetCurrentPosition();
        bool IsMovementDone();
        void Stop();
        void Initialize();
        void HomeAxis();

        int GetStatus();

        void StartHandwheelMode(short axisNum);
        void EndHandwheelMode(short axisNum);
        int GetHandwheelInput();
        bool IsHandwheelMode { get; set; }
        bool IsJoystickMode { get; set; } // Default to automatic mode
        void StartEStopMonitorThread();
        void HandleExternalInput(int inputCode);

        void StartJoystickMode(short axisNum);

        void EndJoystickMode(short axisNum);
        //void StartEStopMonitor();

        Task ResetEStop();
        event Action OnEStopTriggered;

        Task ClearAxisAlarmAsync();
        Task ResetAxisAfterEStopAsync();
        bool IsAxisInAlarm();

        void EStop();


    }

}
