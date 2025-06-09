using UKSF.Api.Core.Exceptions;

namespace UKSF.Api.Exceptions;

[Serializable]
public class DiscordConnectFailedException : UksfException
{
    public DiscordConnectFailedException() : base("Failed to connect to Discord. Please try again", 400) { }
}
