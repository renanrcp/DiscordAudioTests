namespace DiscordAudioTests.Models
{
    public enum PayloadOpcode
    {
        Unknown = -1,
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