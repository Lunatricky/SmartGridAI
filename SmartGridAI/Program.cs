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
        /* Unified Drone AI Controller
 * - C#6 / PB compatible
 * - Uses Remote Control ("RC") and AI Flight block ("AI Flight (Move)")
 * - Turret local locks override IGC relays
 * - GPS home argument: Run "GPS:name:X:Y:Z:color:"
 * - Broadcast new home to swarm via HOME_CHANNEL
 * - Relay targets via TARGET channel
 * - Proper timeouts and AI Flight Behavior toggling
 */

        // ----------------- CONFIG -----------------
        const string REMOTE_NAME = "RC";
        const string AI_OFFENSE_NAME = "AI Offensive (Combat)";
        const string AI_FLIGHT_NAME = "AI Flight (Move)";

        const string TARGET_CHANNEL = "TARGET_COORDS";
        const string HOME_CHANNEL = "HOME_COORDS";

        // default home (change if you want another default)
        Vector3D homePosition = new Vector3D(-4009875, -50250, -792578);

        // timeouts (seconds)
        const double LOCAL_COOLDOWN_SECONDS = 5.0;
        const double RELAY_COOLDOWN_SECONDS = 5.0;

        // arrival radius (meters) that counts as "arrived home"
        const double HOME_ARRIVE_RADIUS = 1000.0;

        // update interval approx (Update10)
        const double DT = 0.1;

        // -----------------------------------------

        IMyRemoteControl rc;
        IMyOffensiveCombatBlock aiOffense;
        IMyFlightMovementBlock aiFlight; // used for ApplyAction("BehaviorOn"/"BehaviorOff")
        List<IMyLargeTurretBase> turrets = new List<IMyLargeTurretBase>();

        IMyBroadcastListener targetListener;
        IMyBroadcastListener homeListener;

        // tracked targets
        Vector3D? lastLocalTarget = null;
        Vector3D? lastRelayTarget = null;

        // timers
        double localCooldown = 0.0;
        double relayLastSeenTimer = RELAY_COOLDOWN_SECONDS + 1.0; // start expired

        // AI Flight behavior state tracking
        bool aiFlightBehaviorOn = true; // assume on initially (we'll try to enable at start)

        public Program()
        {
            // look up blocks by the configured names
            rc = GridTerminalSystem.GetBlockWithName(REMOTE_NAME) as IMyRemoteControl;
            aiOffense = GridTerminalSystem.GetBlockWithName(AI_OFFENSE_NAME) as IMyOffensiveCombatBlock;
            aiFlight = GridTerminalSystem.GetBlockWithName(AI_FLIGHT_NAME) as IMyFlightMovementBlock;

            GridTerminalSystem.GetBlocksOfType(turrets); // populate turret list

            targetListener = IGC.RegisterBroadcastListener(TARGET_CHANNEL);
            targetListener.SetMessageCallback("IGC-TARGET");

            homeListener = IGC.RegisterBroadcastListener(HOME_CHANNEL);
            homeListener.SetMessageCallback("IGC-HOME");

            Runtime.UpdateFrequency = UpdateFrequency.Update10;

            Echo("Drone AI initialized.");
            Echo("RC: " + (rc != null ? rc.CustomName : "NOT FOUND"));
            Echo("AI-Offense: " + (aiOffense != null ? aiOffense.CustomName : "NOT FOUND"));
            Echo("AI-Flight: " + (aiFlight != null ? aiFlight.CustomName : "NOT FOUND"));

            // try to ensure AI flight behavior disabled if RC is already autopiloting
            if (rc != null && rc.IsAutoPilotEnabled)
                SetAIFlightBehavior(false);
            else
                SetAIFlightBehavior(true);   

            EnableAIOffense(); // enable AI offense block if present
        }

        public void Main(string argument, UpdateType updateSource)
        {
            Echo("=== DRONE AI TICK ===");

            EnableAIOffense(); // enable AI offense block if present

            // 1) Handle argument (hotbar Run) for GPS home update
            if (!string.IsNullOrWhiteSpace(argument))
            {
                Vector3D parsed;
                if (TryParseGPS(argument, out parsed))
                {
                    homePosition = parsed;
                    Echo("HOME set via argument: " + FormatVec(homePosition));
                    // broadcast to swarm
                    try { IGC.SendBroadcastMessage(HOME_CHANNEL, homePosition); }
                    catch { }
                }
                else
                {
                    Echo("Argument not GPS or failed parse.");
                }
            }

            // 2) Handle incoming HOME updates from swarm
            if ((updateSource & UpdateType.IGC) != 0)
            {
                while (homeListener.HasPendingMessage)
                {
                    var hmsg = homeListener.AcceptMessage();
                    Vector3D hv;
                    if (TryGetVector(hmsg, out hv))
                    {
                        homePosition = hv;
                        Echo("Received HOME update from swarm: " + FormatVec(homePosition));
                    }
                }
            }

            // 3) Local turret detection (highest priority)
            bool turretsHaveLock = ScanForLocalTarget();

            // 4) If no local lock, accept IGC target updates
            if (!turretsHaveLock)
            {
                FetchIGCTargets(updateSource);
            }

            // 5) Update timers (cooldowns) including relay last-seen aging
            UpdateTimers(turretsHaveLock, DT);

            // 6) turn autopilot OFF and turn ON grid AI
            if (aiOffense.SearchEnemyComponent.FoundEnemyId != null)
            {
                // If turrets have local lock -> enable AI flight behavior, disable RC autopilot
                DisableRemoteControl();
                SetAIFlightBehavior(true);
            }

            // 7) broadcast target coordinates
            if (lastLocalTarget.HasValue)
            {
                RelayLocalTarget(lastLocalTarget.Value);
                return;
            } else if (aiOffense.SearchEnemyComponent.FoundEnemyId != null) 
            {
                RelayLocalTarget(rc.GetPosition());
                return;
            }
            
            // If we have a valid relay target, RC should fly toward it and AI behavior should be OFF
            if (lastRelayTarget.HasValue)
            {
                SetAIFlightBehavior(false);
                FlyToTarget(lastRelayTarget.Value);
                Echo("Mode: RELAY TARGET -> RC autopilot flying to relay target");
                return;
            }

            // if already at home (within radius) enable AI flight behavior
            if (rc != null)
            {
                double distToHome = Vector3D.Distance(rc.GetPosition(), homePosition);
                if (distToHome <= HOME_ARRIVE_RADIUS)
                {
                    DisableRemoteControl();
                    SetAIFlightBehavior(true);
                    Echo("At HOME -> AI flight behavior ON");
                    return;
                }
            }

            // otherwise, fly home
            if (aiOffense.SearchEnemyComponent.FoundEnemyId == null)
            {
                SetAIFlightBehavior(false);
                FlyToTarget(homePosition);
                Echo("Mode: NO TARGET -> Flying home (RC autopilot)");
            }
        }

        // -------------------- Helper: Scan turrets --------------------
        bool ScanForLocalTarget()
        {
            lastLocalTarget = null;

            // ensure turret list up-to-date (in case of grid reconfiguration)
            if (turrets.Count == 0)
                GridTerminalSystem.GetBlocksOfType(turrets);

            foreach (var t in turrets)
            {
                if (t == null) continue;
                if (!t.IsFunctional) continue;

                if (t.HasTarget)
                {
                    MyDetectedEntityInfo info = t.GetTargetedEntity();
                    if (info.EntityId != 0)
                    {
                        lastLocalTarget = info.Position;
                        // reset local cooldown timer
                        localCooldown = LOCAL_COOLDOWN_SECONDS;
                        Echo("Local turret lock at " + FormatVec(info.Position));
                        return true;
                    }
                }
            }

            return false;
        }

        // -------------------- IGC target fetch --------------------
        void FetchIGCTargets(UpdateType updateSource)
        {
            if ((updateSource & UpdateType.IGC) == 0) return;

            bool gotAny = false;
            while (targetListener.HasPendingMessage)
            {
                var msg = targetListener.AcceptMessage();
                Vector3D v;
                if (TryGetVector(msg, out v))
                {
                    lastRelayTarget = v;
                    relayLastSeenTimer = 0.0; // reset last seen timer only when fresh data arrived
                    gotAny = true;
                    Echo("Received relay target: " + FormatVec(v));
                }
            }
            if (!gotAny)
            {
                // no new messages this tick — nothing to do here
            }
        }

        // -------------------- Timers & cooldowns --------------------
        void UpdateTimers(bool turretsLocked, double dt)
        {
            // local turret cooldown
            if (turretsLocked)
            {
                localCooldown = LOCAL_COOLDOWN_SECONDS;
            }
            else if (localCooldown > 0.0)
            {
                localCooldown -= dt;
                if (localCooldown <= 0.0)
                {
                    lastLocalTarget = null;
                    Echo("Local turret lock expired");
                }
            }

            // relay last-seen timer - age only if we have a relay target stored
            if (lastRelayTarget.HasValue)
            {
                relayLastSeenTimer += dt;
                if (relayLastSeenTimer > RELAY_COOLDOWN_SECONDS)
                {
                    lastRelayTarget = null;
                    Echo("Relay timeout expired -> clearing relay target and disabling RC");
                    // make sure RC doesn't continue toward stale target

                    DisableRemoteControl();
                }
            }
        }

        // -------------------- Remote control helpers --------------------
        void FlyToTarget(Vector3D target)
        {
            if (rc == null)
            {
                Echo("RC not found! Can't fly.");
                return;
            }

            try
            {
                rc.ClearWaypoints();
                rc.AddWaypoint(target, "Target");
                if (!rc.IsAutoPilotEnabled)
                    rc.SetAutoPilotEnabled(true);
                Echo("RC moving to " + FormatVec(target));
            }
            catch (Exception e)
            {
                Echo("RC FlyToTarget error: " + e.Message);
            }
        }

        void DisableRemoteControl()
        {
            if (rc == null) return;
            try
            {
                if (rc.IsAutoPilotEnabled)
                    rc.SetAutoPilotEnabled(false);
                rc.ClearWaypoints();
                Echo("RC autopilot disabled");
            }
            catch (Exception e)
            {
                Echo("RC disable error: " + e.Message);
            }
        }

        // -------------------- AI Flight & Offense control --------------------
        void SetAIFlightBehavior(bool enable)
        {
            if (aiFlight == null) return;

            // if already in desired state, skip; we keep a local flag to avoid repeat ApplyAction spam
            if (enable && aiFlightBehaviorOn) return;
            if (!enable && !aiFlightBehaviorOn) return;

            // Try to use the typical terminal actions "BehaviorOn" / "BehaviorOff"
            aiFlight.Enabled = enable;
            aiFlightBehaviorOn = enable;
        }

        void EnableAIOffense()
        {
            if (aiOffense == null) return;
            aiOffense.Enabled = true;
            aiOffense.UpdateTargetInterval = 0;
        }

        // -------------------- Relay local target to swarm --------------------
        void RelayLocalTarget(Vector3D pos)
        {
            IGC.SendBroadcastMessage(TARGET_CHANNEL, pos);
            Echo("Relayed local target to swarm: " + FormatVec(pos));
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

        // GPS parser for "GPS:name:X:Y:Z:color:" format
        bool TryParseGPS(string gps, out Vector3D result)
        {
            result = new Vector3D();
            if (string.IsNullOrWhiteSpace(gps)) return false;
            if (!gps.StartsWith("GPS:")) return false;

            var parts = gps.Split(':');
            if (parts.Length < 6) return false;

            double x, y, z;
            if (!double.TryParse(parts[2], out x)) return false;
            if (!double.TryParse(parts[3], out y)) return false;
            if (!double.TryParse(parts[4], out z)) return false;

            result = new Vector3D(x, y, z);
            return true;
        }

        string FormatVec(Vector3D v)
        {
            return string.Format("{0:0.##},{1:0.##},{2:0.##}", v.X, v.Y, v.Z);
        }


    }
}

