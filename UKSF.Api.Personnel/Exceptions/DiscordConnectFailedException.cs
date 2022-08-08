using UKSF.Api.Shared.Exceptions;

namespace UKSF.Api.Personnel.Exceptions;

[Serializable]
public class DiscordConnectFailedException : UksfException
{
    public DiscordConnectFailedException() : base("Failed to connect to Discord. Please try again", 400) { }
}
