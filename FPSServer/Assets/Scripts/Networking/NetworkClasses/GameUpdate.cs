public class GameUpdate {
    public uint serverTick;
    public sbyte inputBufferOffset;

    public float clientSendTime;
    public float serverSendTime;
    public float serverReceiveTime;

    public MovementState movementState;
    public WorldSnapshot worldSnapshot;
}