namespace IngameScript.Utils
{
    enum DroneState
    {
        // Fly to turret lock or drone broadcast, don't return home until you've found the target or reached the coordinates where target was last locked
        FLYTOTARGET,
        // Switch to grid AI when grid AI locks a target and broadcast current drone coordinates until target is locked by turrets then 
        GRIDAI,
        // Return to home coordinates
        FLYHOME,
        // Fly closer to home to refuel
        REFUEL,
        // Self destruct
        SELFDESTRUCT,
        // Fly in the current relative position
        HOLDTHELINE,
        // Engage only targets within specified distance from mothership
        ENGAGENEARBY,
        // Engage any targets within range or broadcast
        ENGAGEATWILL,
        // REload all blocks on drone
        RELOAD
    }
}
