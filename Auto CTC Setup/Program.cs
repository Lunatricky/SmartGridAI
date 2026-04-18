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
        HashSet<IMyMotorAdvancedStator> AlreadyTouched = new HashSet<IMyMotorAdvancedStator>(); //;)


        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update100;
            Echo($"Setup {SetupTurrets(ref AlreadyTouched)} new turrets.");
        }


        public void Main(string argument, UpdateType updateSource)
        {
            Echo("This script requires the custom turret controller to be on the same subgrid as the weapons");

            if (argument.ToLower() == "reload")
            {
                Echo($"Setup {SetupTurrets(ref AlreadyTouched)} new turrets.");
            }

        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Whitelist", "ProhibitedMemberRule:Prohibited Type Or Member", Justification = "<Pending>")]
        public int SetupTurrets(ref HashSet<IMyMotorAdvancedStator> turrets)
        {
            var turretCount = 0;

            var baseRotors = new List<IMyMotorAdvancedStator>();

            GridTerminalSystem.GetBlocksOfType(baseRotors, r => r.CubeGrid == Me.CubeGrid);

            foreach (var rotor in baseRotors)
            {
                if (turrets.Contains(rotor))
                    continue;

                var grid = rotor.TopGrid;

                if (grid == null)
                    continue;

                IMyMotorAdvancedStator topRotor = null;

                var otherRotors = new List<IMyMotorAdvancedStator>();

                GridTerminalSystem.GetBlocksOfType(otherRotors, r => r.CubeGrid == grid);

                if (otherRotors.Count() == 0)
                    continue;

                topRotor = otherRotors.First();

                if (topRotor == null)
                    continue;

                var topRotorGrid = topRotor.TopGrid;

                if (topRotorGrid == null)
                    continue;

                var blocks = new List<IMyTerminalBlock>();

                GridTerminalSystem.GetBlocksOfType(blocks, block => block.CubeGrid == topRotorGrid);

                IMyTurretControlBlock turretControlBlock = null;

                foreach (var tempblock in blocks)
                {
                    if (tempblock is IMyTurretControlBlock)
                    {
                        turretControlBlock = (IMyTurretControlBlock)tempblock;
                        break;
                    }
                }

                if (turretControlBlock == null)
                    continue;

                turretControlBlock.AzimuthRotor = rotor;
                NameBlock(turretCount, rotor, "Azimuth");
                turretControlBlock.ElevationRotor = topRotor;
                NameBlock(turretCount, topRotor, "Elevation");
                turretControlBlock.AIEnabled = true;
                turretControlBlock.Range = 5000;

                foreach (var block in blocks)
                {
                    if (block is IMyCameraBlock)
                    {
                        turretControlBlock.Camera = block as IMyCameraBlock;
                        NameBlock(turretCount, block);
                        continue;
                    }
                    if (block is IMySmallMissileLauncher)
                    {
                        turretControlBlock.AddTool(block as IMySmallMissileLauncher);
                        NameBlock(turretCount, block);
                        continue;
                    }
                    if (block is IMySmallGatlingGun)
                    {
                        turretControlBlock.AddTool(block as IMySmallGatlingGun);
                        NameBlock(turretCount, block);
                        continue;
                    }
                    if (block is IMyTurretControlBlock || block is IMyMotorAdvancedStator)
                    {
                        NameBlock(turretCount, block);
                        continue;
                    }

                }

                turretCount++;
                turrets.Add(rotor);

            }

            return turretCount;
        }
        private static void NameBlock(int turretCount, IMyTerminalBlock block)
        {
            NameBlock(turretCount, block, "");
        }

        private static void NameBlock(int turretCount, IMyTerminalBlock block, string rotorName)
        {
            if (rotorName != "")
            {
                rotorName = " " + rotorName;
            }
            block.CustomName = block.DefinitionDisplayNameText + rotorName + " #" + (turretCount + 1) + " TCES";
        }
    }
}
