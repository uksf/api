using UKSF.Api.Core.Exceptions;

namespace UKSF.Api.Integrations.Discord.Exceptions;

[Serializable]
public class DiscordException : UksfException
{
    public DiscordException() : base("Timed out whilst trying to connect to discord", 400) { }
}
