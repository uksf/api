using System;

namespace UKSFWebsite.Api {
    public static class Global {
        public const string TOKEN_AUDIENCE = "uksf-audience";
        public const string TOKEN_ISSUER = "uksf-issuer";

        public static IServiceProvider ServiceProvider;
    }
}
