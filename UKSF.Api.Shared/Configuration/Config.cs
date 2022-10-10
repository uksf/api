namespace UKSF.Api.Shared.Configuration;

public class AppSettings
{
    public string Environment { get; set; }
    public string LogsPath { get; set; }
    public string WebUrl { get; set; }
    public string ApiUrl { get; set; }
    public string RedirectUrl { get; set; }
    public string RedirectApiUrl { get; set; }
    public ConnectionStringsConfig ConnectionStrings { get; set; }
    public SecretsConfig Secrets { get; set; }

    public class ConnectionStringsConfig
    {
        public string Database { get; set; }
    }

    public class SecretsConfig
    {
        public string TokenKey { get; set; }
        public DiscordConfig Discord { get; set; }
        public GithubConfig Github { get; set; }
        public EmailConfig Email { get; set; }
        public SteamCmdConfig SteamCmd { get; set; }

        public class DiscordConfig
        {
            public string ClientId { get; set; }
            public string ClientSecret { get; set; }
            public string BotToken { get; set; }
        }

        public class GithubConfig
        {
            public string Token { get; set; }
            public string WebhookSecret { get; set; }
            public string AppPrivateKey { get; set; }
        }

        public class EmailConfig
        {
            public string Username { get; set; }
            public string Password { get; set; }
        }

        public class SteamCmdConfig
        {
            public string Username { get; set; }
            public string Password { get; set; }
        }
    }
}
