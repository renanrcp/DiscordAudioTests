namespace DiscordAudioTests.Models
{
    public enum PayloadOpcode
    {
        Unknown = 0,
        Identify,
        SelectProtocol,
        Ready,
        Heartbeat,
        SessionDescription,
        Speaking,
        HeartbeatAck,
        Resume,
        Hello,
        Resumed
    }
}