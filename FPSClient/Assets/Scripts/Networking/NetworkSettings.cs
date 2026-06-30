public static class NetworkSettings {
    public const int tickRate = 128;
    public const float tickTime = 1f / tickRate;
    
    public const int dataBufferSize = 4096;
    public const int inputHistorySize = tickRate * 2;
}