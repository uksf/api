using UKSF.Api.Core.Exceptions;

namespace UKSF.Api.Integrations.Discord.Exceptions;

[Serializable]
public class DiscordOfflineException : UksfException
{
    public DiscordOfflineException() : base("Timed out whilst trying to connect to discord", 408) { }
}
