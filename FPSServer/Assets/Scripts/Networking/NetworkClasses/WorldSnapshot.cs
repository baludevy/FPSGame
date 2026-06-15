using System.Collections.Generic;

public class WorldSnapshot {
    public uint tick;
    
    public List<PlayerState> playerStates = new List<PlayerState>();
}