public struct TimingInfo {
    public float inputReceiveMargin; // time until latest input will get executed
    public float clientSendTimeAck; // the latest timestamp we received from the client
    public float clientReceiveTime; // calculated locally on the client on arrival
    public float serverReceiveTime; // when did the server receive the latest input packet
    public float serverSendTime; // when did the server send this update
}