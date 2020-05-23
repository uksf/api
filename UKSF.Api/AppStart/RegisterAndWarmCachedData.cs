using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using UKSF.Api.Interfaces.Data;
using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Services.Utility;

namespace UKSF.Api.AppStart {
    public static class RegisterAndWarmCachedData {
        public static void Warm() {
            IServiceProvider serviceProvider = Global.ServiceProvider;

            IAccountDataService accountDataService = serviceProvider.GetService<IAccountDataService>();
            ICommandRequestDataService commandRequestDataService = serviceProvider.GetService<ICommandRequestDataService>();
            ICommentThreadDataService commentThreadDataService = serviceProvider.GetService<ICommentThreadDataService>();
            IDischargeDataService dischargeDataService = serviceProvider.GetService<IDischargeDataService>();
            IGameServersDataService gameServersDataService = serviceProvider.GetService<IGameServersDataService>();
            ILauncherFileDataService launcherFileDataService = serviceProvider.GetService<ILauncherFileDataService>();
            ILoaDataService loaDataService = serviceProvider.GetService<ILoaDataService>();
            INotificationsDataService notificationsDataService = serviceProvider.GetService<INotificationsDataService>();
            IOperationOrderDataService operationOrderDataService = serviceProvider.GetService<IOperationOrderDataService>();
            IOperationReportDataService operationReportDataService = serviceProvider.GetService<IOperationReportDataService>();
            IRanksDataService ranksDataService = serviceProvider.GetService<IRanksDataService>();
            IRolesDataService rolesDataService = serviceProvider.GetService<IRolesDataService>();
            IUnitsDataService unitsDataService = serviceProvider.GetService<IUnitsDataService>();
            IVariablesDataService variablesDataService = serviceProvider.GetService<IVariablesDataService>();

            DataCacheService dataCacheService = serviceProvider.GetService<DataCacheService>();
            dataCacheService.RegisterCachedDataServices(
                new HashSet<ICachedDataService> {
                    accountDataService,
                    commandRequestDataService,
                    commentThreadDataService,
                    dischargeDataService,
                    gameServersDataService,
                    launcherFileDataService,
                    loaDataService,
                    notificationsDataService,
                    operationOrderDataService,
                    operationReportDataService,
                    ranksDataService,
                    rolesDataService,
                    unitsDataService,
                    variablesDataService
                }
            );
            dataCacheService.InvalidateCachedData();
        }
    }
}
