using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace friction_tester
{
    public static class MotionControllerFactory
    {
        /// <summary>
        /// Factory method to create the appropriate motion controller.
        /// </summary>
        /// <param name="isSimulationMode">True to use the simulated controller, False for the real controller.</param>
        /// <returns>An instance of IMotionController.</returns>
        public static IMotionController CreateController(bool isSimulationMode)
        {
            if (isSimulationMode)
            {
                return new SimulatedMotionController();
            }
            else
            {
                return new RealMotionController();
            }
        }
    }
}
