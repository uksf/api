using System.Text;
using AspNet.Security.OpenId;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using UKSF.Api.Core.Configuration;

namespace UKSF.Api.Extensions;

public static class AuthExtensions
{
    public static string TokenAudience => "uksf-audience";
    public static string TokenIssuer => "uksf-issuer";
    public static SymmetricSecurityKey SecurityKey { get; private set; }

    public static IServiceCollection AddUksfAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var appSettings = new AppSettings();
        configuration.GetSection(nameof(AppSettings)).Bind(appSettings);
        SecurityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(appSettings.Secrets.TokenKey));

        services.AddAuthentication(options =>
                    {
                        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                        options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                    }
                )
                .AddJwtBearer(options =>
                    {
                        options.TokenValidationParameters = new TokenValidationParameters
                        {
                            RequireExpirationTime = true,
                            RequireSignedTokens = true,
                            ValidateIssuerSigningKey = true,
                            IssuerSigningKey = SecurityKey,
                            ValidateIssuer = true,
                            ValidIssuer = TokenIssuer,
                            ValidateAudience = true,
                            ValidAudience = TokenAudience,
                            ValidateLifetime = true,
                            ClockSkew = TimeSpan.Zero
                        };
                        options.Audience = TokenAudience;
                        options.ClaimsIssuer = TokenIssuer;
                        options.SaveToken = true;
                        options.Events = new JwtBearerEvents
                        {
                            OnAuthenticationFailed = context =>
                            {
                                context.HttpContext.Items.Add("exception", context.Exception);

                                return Task.CompletedTask;
                            },
                            OnMessageReceived = context =>
                            {
                                var accessToken = context.Request.Query["access_token"];
                                if (!string.IsNullOrEmpty(accessToken) && context.Request.Path.StartsWithSegments("/hub"))
                                {
                                    context.Token = accessToken;
                                }

                                return Task.CompletedTask;
                            }
                        };
                    }
                )
                .AddCookie()
                .AddSteam(options =>
                    {
                        options.ForwardAuthenticate = JwtBearerDefaults.AuthenticationScheme;
                        options.Events = new OpenIdAuthenticationEvents
                        {
                            OnAccessDenied = context =>
                            {
                                context.Response.StatusCode = 401;
                                return Task.CompletedTask;
                            },
                            OnTicketReceived = context =>
                            {
                                var idParts = context.Principal?.Claims
                                                     .First(claim => claim.Type == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")
                                                     .Value.Split('/');
                                var id = idParts?[^1];
                                context.ReturnUri = $"{context.ReturnUri}?id={id}";
                                return Task.CompletedTask;
                            }
                        };
                    }
                );

        return services;
    }
}
