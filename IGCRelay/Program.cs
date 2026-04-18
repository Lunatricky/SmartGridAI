using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Definitions;
using VRageMath;

namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        /*
         * Whip's Turret Based Radar Transponder.
         * 
         * By Natomic
         * 
         * 
         * About
         * -----
         * 
         * This script will broadcast information about the grid it is placed on.
         * It uses the same "network" as Whip's TBR script and is basically just the friendly 
         * broadcasting bit of TBR. This can be used to prevent friendly fire (e.g. Whip's AI Slaving script)
         * 
         * 
         * Config 
         * ------
         * Currently this script has two config options: alligence, and grid type. 
         * 
         * The alligence option affects what type of target you are seen as by the radar. The following are valid values:
         * 
         * 0: Neutral
         * 1: Enemy 
         * 2: Friendly
         * 
         * The grid type option is implicitly set to the current type of grid (large or small) but can be explicitly overriden by 
         * setting "large grid = true/false"
         * 
         * After changing the CustomData, you will need to recompile.
         * 
         * 
         * Thanks
         * ------
         * Thanks to Whip for writing clean, readable code
         * 
         * 
         * Similar scripts
         * ----------------
         * Whip's Turret Based Radar WC Addon: https://steamcommunity.com/sharedfiles/filedetails/?id=2197412752
         * 
         * 
         * License/reuse
         * --------------
         * 
         * This is literally <200 lines of code, use it for whatever just credit me I guess.
         */

        const string IGC_TAG = "MyIGCMessage";
        const string INI_TRANSPONDER = "Radar - Transponder";
        const string SCRIPT_NAME = "WTBR - Transponder";
        const string SCRIPT_VER = "1.1.0";

        private int transmitAnimationStage = 1;
        private const int TRANSMIT_ANIMATION_STAGES = 3;

        private byte transponderRelationship = 2; // Defaults to friendly
        private byte gridType = (byte)MyCubeSize.Large;
        internal bool gridTypeSpecified = false;

        internal static MyIniKey RELATIONSHIP_KH = new MyIniKey(INI_TRANSPONDER, "alligence");
        internal static MyIniKey GRID_KH = new MyIniKey(INI_TRANSPONDER, "large grid");

        private readonly MyIni config = new MyIni();

        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update10;
            ReadConfig();
        }
        internal byte CurrentGridType()
        {
            return (byte)(Me.CubeGrid.GridSizeEnum == MyCubeSize.Large ? 8 : 16);
        }
        internal byte CalcTransponderRelationship()
        {
            return (byte)(transponderRelationship + gridType);
        }


        public void Main()
        {

            Echo($"== {SCRIPT_NAME} v{SCRIPT_VER} ==\nBroadcasting grid information{NextTransmitAnimationStage()}");
            var gridRadius = Me.CubeGrid.WorldVolume.Radius;

            /*
                     * Format of these messages is:
                     * 1. Relationship/alligence
                     *  Basic:
                     *  - Neutral = 0
                     *  - Enemy = 1
                     *  - Friendly = 2
                     * Additional:
                     *  - Locked = 4 (not broadcast, for network notification of lock)
                     *  - LargeGrid = 8
                     *  - SmallGrid = 16
                     * The additional flags are added to the basic, e.g. for large enemy is 1 + 8
                     * 2. EntityId
                     * 3. World relative position
                     * 4. Radius ^ 2 of the grid (for friendly fire detection) (will be zero for non-friendly grids)
                     */
            var myTuple = new MyTuple<byte, long, Vector3D, double>(CalcTransponderRelationship(), Me.CubeGrid.EntityId, Me.WorldVolume.Center, gridRadius * gridRadius);

            IGC.SendBroadcastMessage(IGC_TAG, myTuple);
        }

        private void ReadConfig()
        {
            var parsed = config.TryParse(Me.CustomData);
            if (parsed && Me.CustomData.Length > 0)
            {
                transponderRelationship = config.Get(RELATIONSHIP_KH).ToByte(2);
                if (config.ContainsKey(GRID_KH))
                {
                    gridTypeSpecified = true;
                    gridType = (byte)(config.Get(GRID_KH).ToBoolean() ? 8 : 16);
                }
                else
                {
                    gridType = CurrentGridType();
                }

            }
            SaveConfig();
        }
        private void SaveConfig()
        {
            config.Set(RELATIONSHIP_KH, transponderRelationship);
            if (gridTypeSpecified)
            {
                config.Set(GRID_KH, gridType);
            }

            Me.CustomData = config.ToString();
        }
        private string NextTransmitAnimationStage()
        {
            if (transmitAnimationStage == TRANSMIT_ANIMATION_STAGES + 1)
            {
                transmitAnimationStage = 1;
            }
            var prev = transmitAnimationStage;
            ++transmitAnimationStage;
            return new string('.', prev);

        }
    }
}
