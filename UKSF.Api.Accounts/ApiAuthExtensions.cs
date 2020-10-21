using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AspNet.Security.OpenId;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using Microsoft.IdentityModel.Tokens;
using UKSF.Api.Accounts.Services.Auth;

namespace UKSF.Api.Accounts {
    public static class ApiAuthExtensions {
        public static string TokenAudience => "uksf-audience";
        public static string TokenIssuer => "uksf-issuer";
        public static SymmetricSecurityKey SecurityKey { get; private set; }

        public static IServiceCollection AddUksfAuth(this IServiceCollection services, IConfiguration configuration) {
            SecurityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration.GetSection("Secrets")["tokenKey"]));

            services.AddTransient<ILoginService, LoginService>();
            services.AddSingleton<ISessionService, SessionService>();

            services.AddAuthentication(
                        options => {
                            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                            options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                        }
                    )
                    .AddJwtBearer(
                        options => {
                            options.TokenValidationParameters = new TokenValidationParameters {
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
                            options.Events = new JwtBearerEvents {
                                OnMessageReceived = context => {
                                    StringValues accessToken = context.Request.Query["access_token"];
                                    if (!string.IsNullOrEmpty(accessToken) && context.Request.Path.StartsWithSegments("/hub")) {
                                        context.Token = accessToken;
                                    }

                                    return Task.CompletedTask;
                                }
                            };
                        }
                    )
                    .AddCookie()
                    .AddSteam(
                        options => {
                            options.ForwardAuthenticate = JwtBearerDefaults.AuthenticationScheme;
                            options.Events = new OpenIdAuthenticationEvents {
                                OnAccessDenied = context => {
                                    context.Response.StatusCode = 401;
                                    return Task.CompletedTask;
                                },
                                OnTicketReceived = context => {
                                    string[] idParts = context.Principal?.Claims.First(claim => claim.Type == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier").Value.Split('/');
                                    string id = idParts?[^1];
                                    context.ReturnUri = $"{context.ReturnUri}?id={id}";
                                    return Task.CompletedTask;
                                }
                            };
                        }
                    );

            return services;
        }
    }
}
