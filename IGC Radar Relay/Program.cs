// =======================================================
// Stealth Radar Bridge → Whip's Turret Radar (via Argument)
// Fully C#6 compatible for Space Engineers
// =======================================================

using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Text;
using VRage;
using VRage.Game.GUI.TextPanel;
using VRageMath;

namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        // ====================== CONFIG ======================
        const string Version = "1.2";

        string RadarPBName = "Relay";        // Change to match your radar PB name

        double TimeoutSeconds = 30.0;

        string HistoryLCDTag = "[History]";

        // ====================== DATA ======================
        struct CachedTarget
        {
            public Vector3D Position;
            public long SourceId;
            public long GridId;
            public string GridName;
            public string FactionTag;
            public double LastSeen;
            public double OwnerID;
            public string IFF;
        }

        IMyProgrammableBlock me;

        Dictionary<long, CachedTarget> ActiveTargets = new Dictionary<long, CachedTarget>();
        List<CachedTarget> HistoryTargets = new List<CachedTarget>();

        // ====================== BLOCKS ======================
        IMyProgrammableBlock RadarPB;
        List<IMyTextSurface> HistoryLCDs = new List<IMyTextSurface>();

        // ====================== TIMING ======================
        double _timeAccumulator = 0.0;

        // IGC Listeners
        const string RadarListenerChannel = "Radar_Broadcast";
        IMyBroadcastListener _radarListener;

        const string IGC_IFF_MSG = "IGC_IFF_MSG";
        IMyBroadcastListener _whipListener;


        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update1;

            FindBlocks();

            // Register listeners
            _radarListener = IGC.RegisterBroadcastListener(RadarListenerChannel);
            _radarListener.SetMessageCallback(RadarListenerChannel);

            _whipListener = IGC.RegisterBroadcastListener(IGC_IFF_MSG);
            _whipListener.SetMessageCallback(IGC_IFF_MSG);
        }

        public void Main(string arg, UpdateType updateSource)
        {
            // 2) Handle incoming HOME updates from swarm
            if ((updateSource & UpdateType.IGC) != 0)
            {
                while (_radarListener.HasPendingMessage)
                {
                    MyIGCMessage hmsg = _radarListener.AcceptMessage();
                    AddTarget(hmsg.Source, hmsg);
                }

                while (_whipListener.HasPendingMessage)
                {
                    MyIGCMessage hmsg = _whipListener.AcceptMessage();
                    Echo("IGCMessage: " + hmsg.Data);
                    IGC.SendBroadcastMessage(IGC_IFF_MSG, hmsg.Data.ToString());
                }
            }

            UpdateAndSendToRadar();
            UpdateHistoryLCD();

            Echo("Stealth Bridge v" + Version + " Ready");
            Echo("History LCDs: " + HistoryLCDs.Count);
            Echo("Active Targets: " + ActiveTargets.Count);

            Echo("------------");
            Echo("Position: " + t.Item1);
            Echo("GridId: " + t.Item2);
            Echo("GridName: " + t.Item3);
            Echo("FactionTag: " + t.Item4);
            Echo("OwnerID: " + t.Item5);
            Echo("IFF: " + t.Item6);

            foreach (var kvp in ActiveTargets)
            {
                Echo("------------");
                Echo("kvp key: " + kvp.Key);

                Echo("Position: " + kvp.Value.Position);
                Echo("GridId: " + kvp.Value.GridId);
                Echo("GridName: " + kvp.Value.GridName);
                Echo("FactionTag: " + kvp.Value.FactionTag);
                Echo("OwnerID: " + kvp.Value.OwnerID);
                Echo("LastSeen: " + kvp.Value.LastSeen);
                Echo("IFF: " + kvp.Value.IFF);
                Echo("SourceId: " + kvp.Value.SourceId);
            }
            Echo("------------");
        }

        MyTuple<Vector3D, long, string, string, long, string> t;
        void AddTarget(long sourceId, object data)
        {
            Vector3D position = Vector3D.Zero;
            long gridId = 0;
            string gridName = "Unknown";
            string factionTag = "?";
            double ownerID = 0;
            string iff = "";

            // Try to parse as rich MyTuple (recommended)
            if (data is MyTuple<Vector3D, long, string, string, long, string>)
            {
                Echo("MyTuple");
                t = (MyTuple<Vector3D, long, string, string, long, string>)data;
                position = t.Item1;
                gridId = t.Item2;
                gridName = t.Item3 ?? "Unknown";
                factionTag = t.Item4 ?? "?";
                ownerID = t.Item5;
                iff = t.Item6 ?? "";
            }

            CachedTarget target = new CachedTarget
            {
                Position = position,
                SourceId = sourceId,
                GridId = gridId,
                GridName = gridName,
                FactionTag = factionTag,
                LastSeen = _timeAccumulator,
                OwnerID = ownerID,
                IFF = iff
            };
            Echo("***************DEBUG***************");
            ActiveTargets[gridId] = target;
        }

        void FindBlocks()
        {
            me = Me;

            RadarPB = GridTerminalSystem.GetBlockWithName(RadarPBName) as IMyProgrammableBlock;

            if (RadarPB == null)
            {
                Echo($"WARNING: Radar PB '{RadarPBName}' not found!");
            }

            AddLCDsToList(HistoryLCDs, HistoryLCDTag);
        }

        private void AddLCDsToList(List<IMyTextSurface> lcds, string LCD_TAG)
        {

            // LCDs
            var blocks = new List<IMyTerminalBlock>();
            GridTerminalSystem.GetBlocksOfType<IMyTextSurfaceProvider>(blocks, block =>
                block.IsSameConstructAs(me) &&
                block.CustomName.Contains(LCD_TAG)
            );

            foreach (IMyTextSurfaceProvider surfaceProvider in blocks)
            {
                // Only take the first surface (index 0)
                if (surfaceProvider.SurfaceCount > 0)
                {
                    var surface = surfaceProvider.GetSurface(0);

                    lcds.Add(SetupSurface(surface));
                }
            }
        }

        private static IMyTextSurface SetupSurface(IMyTextSurface surface)
        {
            surface.ContentType = ContentType.TEXT_AND_IMAGE;
            surface.Font = "DEBUG";
            surface.FontSize = 1.5f;
            surface.Alignment = TextAlignment.LEFT;
            return surface;
        }

        void UpdateAndSendToRadar()
        {
            _timeAccumulator += Runtime.TimeSinceLastRun.TotalSeconds;

            double now = _timeAccumulator;
            var toRemove = new List<long>();

            foreach (var kvp in ActiveTargets)
            {
                if (now - kvp.Value.LastSeen > TimeoutSeconds)
                {
                    HistoryTargets.Add(kvp.Value);
                    toRemove.Add(kvp.Key);
                }
            }

            foreach (var id in toRemove)
                ActiveTargets.Remove(id);

            SendToRadarViaArgument();
        }

        void SendToRadarViaArgument()
        {
            if (RadarPB == null || !RadarPB.IsFunctional) return;

            foreach (var ct in ActiveTargets)
            {
                var sb = new StringBuilder();
                sb.Append(ct.Value.IFF).Append(" ")
                  .Append(ct.Key);

                string command = sb.ToString().Trim();
                if (!string.IsNullOrEmpty(command))
                {
                    RadarPB.TryRun(command);
                }
            }
        }

        void UpdateHistoryLCD()
        {
            if (HistoryLCDs.Count == 0) return;

            var sb = new StringBuilder();
            sb.AppendLine("=== REMOVED TARGETS HISTORY ===");
            sb.AppendLine("Total: " + HistoryTargets.Count + "\n");

            for (int i = HistoryTargets.Count - 1; i >= 0; i--)
            {
                var t = HistoryTargets[i];
                sb.AppendLine(t.IFF  + " " + t.GridName);
                sb.AppendLine("   ID: " + t.GridId);
                sb.AppendLine("   Pos: " + t.Position.X.ToString("F0") + ", " + t.Position.Y.ToString("F0") + ", " + t.Position.Z.ToString("F0"));
                sb.AppendLine("   Faction: " + t.FactionTag);
                sb.AppendLine("────────────────────────────");
            }

            foreach (var lcd in HistoryLCDs)
            {
                lcd.WriteText(sb.ToString());
            }
        }
    }
}