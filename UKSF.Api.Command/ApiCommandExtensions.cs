using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using UKSF.Api.Command.Context;
using UKSF.Api.Command.EventHandlers;
using UKSF.Api.Command.Models;
using UKSF.Api.Command.Services;
using UKSF.Api.Command.Signalr.Hubs;
using UKSF.Api.Shared.Events;

namespace UKSF.Api.Command {
    public static class ApiCommandExtensions {
        public static IServiceCollection AddUksfCommand(this IServiceCollection services) => services.AddContexts().AddEventBuses().AddEventHandlers().AddServices();

        private static IServiceCollection AddContexts(this IServiceCollection services) =>
            services.AddSingleton<ICommandRequestArchiveDataService, CommandRequestArchiveDataService>()
                    .AddSingleton<ICommandRequestDataService, CommandRequestDataService>()
                    .AddSingleton<IDischargeDataService, DischargeDataService>()
                    .AddSingleton<ILoaDataService, LoaDataService>()
                    .AddSingleton<IOperationOrderDataService, OperationOrderDataService>()
                    .AddSingleton<IOperationReportDataService, OperationReportDataService>();

        private static IServiceCollection AddEventBuses(this IServiceCollection services) =>
            services.AddSingleton<IDataEventBus<CommandRequest>, DataEventBus<CommandRequest>>()
                    .AddSingleton<IDataEventBus<DischargeCollection>, DataEventBus<DischargeCollection>>()
                    .AddSingleton<IDataEventBus<Opord>, DataEventBus<Opord>>()
                    .AddSingleton<IDataEventBus<Oprep>, DataEventBus<Oprep>>();

        private static IServiceCollection AddEventHandlers(this IServiceCollection services) => services.AddSingleton<ICommandRequestEventHandler, CommandRequestEventHandler>();

        private static IServiceCollection AddServices(this IServiceCollection services) =>
            services.AddSingleton<IChainOfCommandService, ChainOfCommandService>()
                    .AddTransient<ICommandRequestCompletionService, CommandRequestCompletionService>()
                    .AddTransient<ICommandRequestService, CommandRequestService>()
                    .AddTransient<IDischargeService, DischargeService>()
                    .AddTransient<ILoaService, LoaService>()
                    .AddTransient<IOperationOrderService, OperationOrderService>()
                    .AddTransient<IOperationReportService, OperationReportService>();

        public static void AddUksfCommandSignalr(this IEndpointRouteBuilder builder) {
            builder.MapHub<CommandRequestsHub>($"/hub/{CommandRequestsHub.END_POINT}");
        }
    }
}
