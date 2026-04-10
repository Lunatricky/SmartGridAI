// =======================================================
// Reliable Short-Burst Relay Transmitter
// Sends grid center on "test" channel with minimal visibility
// =======================================================

using Sandbox.ModAPI.Ingame;
using VRage;
using VRageMath;

namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        IMyRadioAntenna Antenna;
        IMyBeacon Beacon;

        // Burst timing
        double _sendInterval = 2.0;        // How often to send (seconds)
        double _timeSinceLastSend = 0.0;

        double _burstLength = 0.12;        // 120ms burst - good balance between reliability and stealth
        double _burstTimer = 0.0;
        bool _isBursting = false;

        // Your original variables
        Vector3D lastRelayTarget;
        long source;
        IMyBroadcastListener targetListener;
        const string RADAR_CHANNEL = "IGC_IFF_MSG";

        public Program()
        {
            Antenna = GridTerminalSystem.GetBlockWithName("Antenna") as IMyRadioAntenna;
            Beacon = GridTerminalSystem.GetBlockWithName("Beacon") as IMyBeacon;

            if (Antenna == null)
                Echo("ERROR: Antenna 'Antenna' not found!");

            targetListener = IGC.RegisterBroadcastListener(RADAR_CHANNEL);
            targetListener.SetMessageCallback(RADAR_CHANNEL);

            Runtime.UpdateFrequency = UpdateFrequency.Update1;   // Important: Update1 for precise timing
        }

        public void Main(string arg, UpdateType updateSource)
        {                        
            double dt = Runtime.TimeSinceLastRun.TotalSeconds;

            _timeSinceLastSend += dt;

            // Time to do a burst?
            if (_timeSinceLastSend >= _sendInterval)
            {
                StartBurst();
                _timeSinceLastSend = 0.0;
            }

            // Handle active burst
            if (_isBursting)
            {
                _burstTimer += dt;
                if (_burstTimer >= _burstLength)
                {
                    EndBurst();
                }
            }

            // Process any incoming IGC messages
            if ((updateSource & UpdateType.IGC) != 0)
            {
                FetchIGCTargets(updateSource);
            }

            // Debug info
            Echo("Source: " + source);
            Echo("Received relay target: " + FormatVec(lastRelayTarget));
        }

        void StartBurst()
        {
            if (Antenna == null) return;

            Antenna.Enabled = true;
            Antenna.EnableBroadcasting = true;

            _isBursting = true;
            _burstTimer = 0.0;

            BoundingSphereD worldVolume = Me.CubeGrid.WorldVolume;
            var gridCenter = worldVolume.Center;
            var gridRadius = worldVolume.Radius;

            var myTuple = new MyTuple<byte, long, Vector3D, double>(2, Me.CubeGrid.EntityId, gridCenter, gridRadius * gridRadius);

            IGC.SendBroadcastMessage(RADAR_CHANNEL, myTuple);
        }

        void EndBurst()
        {
            if (Antenna != null)
            {
                Antenna.EnableBroadcasting = false;
                // Antenna.Enabled = false;     // Uncomment if you want maximum stealth (slower re-enable)
            }
            _isBursting = false;
        }

        // -------------------- IGC target fetch --------------------
        void FetchIGCTargets(UpdateType updateSource)
        {
            if ((updateSource & UpdateType.IGC) == 0) return;

            while (targetListener.HasPendingMessage)
            {
                MyIGCMessage msg = targetListener.AcceptMessage();

                Vector3D v;
                if (TryGetVector(msg, out v))
                {
                    source = msg.Source;
                    lastRelayTarget = v;
                }
            }
        }

        // -------------------- Parsing helpers --------------------
        bool TryGetVector(MyIGCMessage msg, out Vector3D v)
        {
            v = new Vector3D();
            if (msg.Data is Vector3D)
            {
                v = (Vector3D)msg.Data;
                return true;
            }

            string s = msg.Data as string;
            if (s != null)
                return TryParseSimpleVector(s, out v);

            return false;
        }

        bool TryParseSimpleVector(string s, out Vector3D v)
        {
            v = new Vector3D();
            if (string.IsNullOrWhiteSpace(s)) return false;

            var parts = s.Split(',');
            if (parts.Length != 3) return false;

            double x, y, z;
            if (!double.TryParse(parts[0], out x)) return false;
            if (!double.TryParse(parts[1], out y)) return false;
            if (!double.TryParse(parts[2], out z)) return false;

            v = new Vector3D(x, y, z);
            return true;
        }

        string FormatVec(Vector3D v)
        {
            return string.Format("{0:0.##},{1:0.##},{2:0.##}", v.X, v.Y, v.Z);
        }
    }
}