using AngleSharp;
using Microsoft.Extensions.DependencyInjection;
using UKSF.Api.Interfaces.Personnel;
using UKSF.Api.Services.Personnel;
using UKSF.Api.Accounts.Services;
using UKSF.Api.Data.Utility;
using UKSF.Api.Interfaces.Data;
using UKSF.Api.Interfaces.Utility;
using UKSF.Api.Interfaces.Utility.ScheduledActions;
using UKSF.Api.Services.Utility.ScheduledActions;

namespace UKSF.Api.Accounts {
    public static class ApiAccountsExtensions {
        public static IServiceCollection AddUksfAccounts(this IServiceCollection services, IConfiguration configuration) {
            services.AddTransient<IDisplayNameService, DisplayNameService>();
            services.AddTransient<IConfirmationCodeService, ConfirmationCodeService>();
            services.AddTransient<IConfirmationCodeDataService, ConfirmationCodeDataService>();
            services.AddTransient<IDeleteExpiredConfirmationCodeAction, DeleteExpiredConfirmationCodeAction>();

            return services;
        }
    }
}
