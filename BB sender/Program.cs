// =======================================================
// Frame-Aware Stealth Burst Transmitter
// More reliable short bursts using frame counting
// =======================================================

using Sandbox.ModAPI.Ingame;
using VRageMath;

namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        IMyRadioAntenna Antenna;

        int _sendIntervalFrames = 120;      // Send every 120 frames (~2 seconds at 60fps)
        int _frameCounter = 0;

        int _burstLengthFrames = 8;         // 7 frames ≈ 116 ms - very good balance
        int _burstFrameCounter = 0;
        bool _isBursting = false;

        public Program()
        {
            Antenna = GridTerminalSystem.GetBlockWithName("Stealth Antenna") as IMyRadioAntenna;

            if (Antenna == null)
                Echo("ERROR: 'Stealth Antenna' not found!");

            Runtime.UpdateFrequency = UpdateFrequency.Update1;   // Must be Update1 for frame precision
        }

        public void Main(string arg, UpdateType updateSource)
        {
            _frameCounter++;

            // Time to send a new burst?
            if (_frameCounter >= _sendIntervalFrames)
            {
                StartBurst();
                _frameCounter = 0;
            }

            // Handle active burst
            if (_isBursting)
            {
                _burstFrameCounter++;

                if (_burstFrameCounter >= _burstLengthFrames)
                {
                    EndBurst();
                }
            }

            // Optional debug
            // Echo("Frame: " + _frameCounter + " | Bursting: " + _isBursting);
        }

        void StartBurst()
        {
            if (Antenna == null) return;

            Antenna.Enabled = true;
            Antenna.EnableBroadcasting = true;

            _isBursting = true;
            _burstFrameCounter = 0;

            // === SEND YOUR MESSAGE ===
            Vector3D center = Me.CubeGrid.WorldVolume.Center;

            IGC.SendBroadcastMessage("StealthRelay", center);
        }

        void EndBurst()
        {
            if (Antenna != null)
            {
                Antenna.EnableBroadcasting = false;
                // Antenna.Enabled = false;     // Uncomment for maximum stealth (slower reactivation)
            }

            _isBursting = false;
        }
    }
}