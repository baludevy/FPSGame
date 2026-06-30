public static class NetcodeState {
    // input arrival time to execution (server)
    public static float targetInputMargin;
    
    // snapshot arrival time to consumption time (client)
    public static float targetReceiveMargin; 

    public static uint inputRedundancy;
}