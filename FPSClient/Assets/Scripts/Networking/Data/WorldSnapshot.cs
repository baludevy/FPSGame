public struct WorldSnapshot {
    public uint serverTick;
    public float serverSendTime;
    public float clientReceiveTime;
    public bool consumed;
    
    public int playerStatesCount;
    public PlayerState[] playerStates;
}