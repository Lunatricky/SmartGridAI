// =======================================================
// Stealth Receiver - Action Relay Bridge (C#6)
// Receives short-burst messages and forwards to your radar bridge
// =======================================================

using Sandbox.ModAPI.Ingame;
using System;
using VRageMath;

namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        IMyProgrammableBlock RadarBridgePB;   // Name your bridge PB "Radar Bridge"

        IMyBroadcastListener RelayListener;

        public Program()
        {
            RadarBridgePB = GridTerminalSystem.GetBlockWithName("Radar Bridge") as IMyProgrammableBlock;

            if (RadarBridgePB == null)
                Echo("WARNING: Radar Bridge PB not found!");

            RelayListener = IGC.RegisterBroadcastListener("StealthRelay");
            RelayListener.SetMessageCallback("StealthRelay");

            Runtime.UpdateFrequency = UpdateFrequency.Update1;
        }

        public void Main(string arg, UpdateType updateSource)
        {
            if ((updateSource & UpdateType.IGC) != 0)
            {
                ProcessRelayMessages();
            }
        }

        void ProcessRelayMessages()
        {
            while (RelayListener.HasPendingMessage)
            {
                MyIGCMessage msg = RelayListener.AcceptMessage();

                // C#6 safe way to check if data is string
                string dataStr = msg.Data as string;

                if (dataStr != null && dataStr.StartsWith("POS|"))
                {
                    string[] parts = dataStr.Split('|');
                    if (parts.Length >= 3)
                    {
                        string posStr = parts[1];
                        string gridName = parts[2];

                        Vector3D pos;
                        if (TryParseVector(posStr, out pos))
                        {
                            // Forward to your bridge PB using argument
                            string command = "ADD F|" + msg.Source + "|" + gridName.Replace("|", "_") + "|"
                                           + pos.X.ToString("0") + ","
                                           + pos.Y.ToString("0") + ","
                                           + pos.Z.ToString("0");

                            if (RadarBridgePB != null)
                                RadarBridgePB.TryRun(command);

                            Echo("Received stealth position from " + gridName);
                        }
                    }
                }
            }
        }

        bool TryParseVector(string s, out Vector3D v)
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
    }
}