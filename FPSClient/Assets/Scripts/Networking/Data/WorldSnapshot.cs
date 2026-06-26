using System.Collections.Generic;

public class WorldSnapshot {
    public uint serverTick;
    public float serverSendTime;
    public float clientReceiveTime;
    public bool consumed;
    
    public List<PlayerState> playerStates = new List<PlayerState>();
}