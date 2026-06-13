using System.Collections.Generic;

public class WorldSnapshot {
    public uint serverTick;
    public sbyte inputBufferOffset;

    public float clientSendTime;
    public float serverSendTime;
    public float serverReceiveTime;

    public List<PlayerState> playerStates = new List<PlayerState>();
}