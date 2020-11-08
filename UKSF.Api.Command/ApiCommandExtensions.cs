using Microsoft.Extensions.DependencyInjection;
using UKSF.Api.Base.Events;
using UKSF.Api.Command.Context;
using UKSF.Api.Command.EventHandlers;
using UKSF.Api.Command.Models;
using UKSF.Api.Command.Services;

namespace UKSF.Api.Command {
    public static class ApiCommandExtensions {
        public static IServiceCollection AddUksfCommand(this IServiceCollection services) => services.AddContexts().AddEventBuses().AddEventHandlers().AddServices();

        private static IServiceCollection AddContexts(this IServiceCollection services) =>
            services.AddSingleton<ICommandRequestArchiveDataService, CommandRequestArchiveDataService>()
                    .AddSingleton<ICommandRequestDataService, CommandRequestDataService>()
                    .AddSingleton<IOperationOrderDataService, OperationOrderDataService>()
                    .AddSingleton<IOperationReportDataService, OperationReportDataService>();

        private static IServiceCollection AddEventBuses(this IServiceCollection services) =>
            services.AddSingleton<IDataEventBus<CommandRequest>, DataEventBus<CommandRequest>>()
                    .AddSingleton<IDataEventBus<Opord>, DataEventBus<Opord>>()
                    .AddSingleton<IDataEventBus<Oprep>, DataEventBus<Oprep>>();

        private static IServiceCollection AddEventHandlers(this IServiceCollection services) => services.AddSingleton<ICommandRequestEventHandler, CommandRequestEventHandler>();

        private static IServiceCollection AddServices(this IServiceCollection services) =>
            services.AddSingleton<IChainOfCommandService, ChainOfCommandService>()
                    .AddTransient<ICommandRequestCompletionService, CommandRequestCompletionService>()
                    .AddTransient<ICommandRequestService, CommandRequestService>()
                    .AddTransient<IOperationOrderService, OperationOrderService>()
                    .AddTransient<IOperationReportService, OperationReportService>();
    }
}
