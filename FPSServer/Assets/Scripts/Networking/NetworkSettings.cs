public static class NetworkSettings
{
    public const int dataBufferSize = 4096;
    public const int tickRate = 128;
    public const float tickTime = 1f / tickRate;
    public const int inputBufferSize = 1024;

    public const float maxInputMarginTime = 0.2f; // inputs later than this relative to execution time will be dropped
    public const float maxLagCompensationTime = 0.2f;
}