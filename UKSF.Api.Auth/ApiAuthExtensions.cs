using System.Text;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using UKSF.Api.Auth.Commands;
using UKSF.Api.Auth.Services;
using UKSF.Api.Base.Configuration;

namespace UKSF.Api.Auth;

public static class ApiAuthExtensions
{
    public static string TokenAudience => "uksf-audience";
    public static string TokenIssuer => "uksf-issuer";
    public static SymmetricSecurityKey SecurityKey { get; private set; }

    public static IServiceCollection AddUksfAuth(this IServiceCollection services, IConfiguration configuration)
    {
        var appSettings = new AppSettings();
        configuration.GetSection(nameof(AppSettings)).Bind(appSettings);
        SecurityKey = new(Encoding.UTF8.GetBytes(appSettings.Secrets.TokenKey));

        return services.AddContexts().AddEventHandlers().AddServices().AddCommands().AddQueries().AddAuthentication();
    }

    private static IServiceCollection AddContexts(this IServiceCollection services)
    {
        return services;
    }

    private static IServiceCollection AddEventHandlers(this IServiceCollection services)
    {
        return services;
    }

    private static IServiceCollection AddServices(this IServiceCollection services)
    {
        return services.AddSingleton<ILoginService, LoginService>().AddSingleton<IPermissionsService, PermissionsService>();
    }

    private static IServiceCollection AddCommands(this IServiceCollection services)
    {
        return services.AddSingleton<IRequestPasswordResetCommand, RequestPasswordResetCommand>().AddSingleton<IResetPasswordCommand, ResetPasswordCommand>();
    }

    private static IServiceCollection AddQueries(this IServiceCollection services)
    {
        return services;
    }

    private static IServiceCollection AddAuthentication(this IServiceCollection services)
    {
        services.AddAuthentication(
                    options =>
                    {
                        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                        options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                    }
                )
                .AddJwtBearer(
                    options =>
                    {
                        options.TokenValidationParameters = new()
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
                        options.Events = new()
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
                .AddSteam(
                    options =>
                    {
                        options.ForwardAuthenticate = JwtBearerDefaults.AuthenticationScheme;
                        options.Events = new()
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
