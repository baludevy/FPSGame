public class InputData {
    public uint tick;
    public float interpolationFactor;
    public float renderTick;

    public float x;
    public float y;

    public float pitch;
    public float yaw;

    public Buttons buttons;
}

[System.Flags]
public enum Buttons : byte {
    None = 0,
    Jump = 1 << 0,
    Crouch = 1 << 1,
    Shoot = 1 << 2,
}