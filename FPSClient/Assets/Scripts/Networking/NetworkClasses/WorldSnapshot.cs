using System.Collections.Generic;

public class WorldSnapshot {
    public int serverTick;
    public int bufferSlack;

    public List<PlayerState> playerStates = new List<PlayerState>();
}