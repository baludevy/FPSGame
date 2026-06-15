using System.Collections.Generic;

public class WorldSnapshot {
    public uint serverTick;
    
    public List<PlayerState> playerStates = new List<PlayerState>();
}