using UKSF.Api.Core.Exceptions;

namespace UKSF.Api.Discord.Exceptions;

[Serializable]
public class DiscordOfflineException : UksfException
{
    public DiscordOfflineException() : base("Timed out whilst trying to connect to discord", 408) { }
}
