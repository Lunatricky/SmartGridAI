using IngameScript.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IngameScript
{
    partial class DroneStateMachine
    {
        public DroneStateMachine(DroneState droneState)
        {
            switch (droneState)
            {
                case DroneState.FLYTOTARGET:
                    break;
                case DroneState.GRIDAI:
                    break;
                case DroneState.FLYHOME:
                    break;
                case DroneState.REFUEL:
                    break;
                case DroneState.SELFDESTRUCT:
                    break;
                case DroneState.HOLDTHELINE:
                    break;
                case DroneState.ENGAGENEARBY:
                    break;
                case DroneState.ENGAGEATWILL:
                    break;
                case DroneState.RELOAD:
                    break;
            }
        }
    }
}
