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
        Task MoveToPositionAsync(double position, int maxVelocity, double acceleration);
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

        void HandleExternalInput(int inputCode);

        void StartJoystickMode(short axisNum);

        void EndJoystickMode(short axisNum);
        void StartEStopMonitor();

        void ResetEStop();
        event Action OnEStopTriggered;

    }

}
