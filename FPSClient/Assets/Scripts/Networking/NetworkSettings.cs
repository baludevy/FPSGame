public static class NetworkSettings
{
    public const int dataBufferSize = 4096;
    public const int tickRate = 64;
    public const float tickTime = 1f / tickRate;
    public const int inputBufferSize = 1024;
    
    public static int inputRedundancy = 1;
    public static int targetInpBufferOffset = 1;

    public static float interpTime = 0.015625f;
}