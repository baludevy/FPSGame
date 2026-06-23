public class GameUpdate {
    public uint serverTick;
    public float serverReceiveMargin;
    public float serverInputJitter;

    public float clientSendTime;
    public float serverSendTime;
    public float serverReceiveTime;

    public MovementState movementState;
    public WorldSnapshot worldSnapshot;
}