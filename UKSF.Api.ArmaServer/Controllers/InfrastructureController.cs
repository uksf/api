﻿using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using UKSF.Api.Admin.Extensions;
using UKSF.Api.Admin.Services;
using UKSF.Api.ArmaServer.Commands;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.ArmaServer.Queries;
using UKSF.Api.Shared;
using UKSF.Api.Shared.Events;

namespace UKSF.Api.ArmaServer.Controllers
{
    [Route("servers/infrastructure"), Permissions(Permissions.ADMIN)]
    public class InfrastructureController : ControllerBase
    {
        private readonly IGetCurrentServerInfrastructureQuery _getCurrentServerInfrastructureQuery;
        private readonly IGetInstalledServerInfrastructureQuery _getInstalledServerInfrastructureQuery;
        private readonly IGetLatestServerInfrastructureQuery _getLatestServerInfrastructureQuery;
        private readonly ILogger _logger;
        private readonly IUpdateServerInfrastructureCommand _updateServerInfrastructureCommand;
        private readonly IVariablesService _variablesService;

        public InfrastructureController(
            IGetLatestServerInfrastructureQuery getLatestServerInfrastructureQuery,
            IGetCurrentServerInfrastructureQuery getCurrentServerInfrastructureQuery,
            IGetInstalledServerInfrastructureQuery getInstalledServerInfrastructureQuery,
            IUpdateServerInfrastructureCommand updateServerInfrastructureCommand,
            IVariablesService variablesService,
            ILogger logger
        )
        {
            _getLatestServerInfrastructureQuery = getLatestServerInfrastructureQuery;
            _getCurrentServerInfrastructureQuery = getCurrentServerInfrastructureQuery;
            _getInstalledServerInfrastructureQuery = getInstalledServerInfrastructureQuery;
            _updateServerInfrastructureCommand = updateServerInfrastructureCommand;
            _variablesService = variablesService;
            _logger = logger;
        }

        [HttpGet("isUpdating")]
        public bool IsUpdating()
        {
            return _variablesService.GetVariable("SERVER_INFRA_UPDATING").AsBool();
        }

        [HttpGet("latest")]
        public async Task<ServerInfrastructureLatest> GetLatest()
        {
            return await _getLatestServerInfrastructureQuery.ExecuteAsync();
        }

        [HttpGet("current")]
        public async Task<ServerInfrastructureCurrent> GetCurrent()
        {
            return await _getCurrentServerInfrastructureQuery.ExecuteAsync();
        }

        [HttpGet("installed")]
        public async Task<ServerInfrastructureInstalled> GetInstalled()
        {
            return await _getInstalledServerInfrastructureQuery.ExecuteAsync();
        }

        [HttpGet("update")]
        public async Task<ServerInfrastructureUpdate> Update()
        {
            var before = await _getInstalledServerInfrastructureQuery.ExecuteAsync();
            var result = await _updateServerInfrastructureCommand.ExecuteAsync();
            var after = await _getInstalledServerInfrastructureQuery.ExecuteAsync();

            _logger.LogInfo($"Server infrastructure updated from version {before.InstalledVersion} to {after.InstalledVersion}");

            return new() { NewVersion = after.InstalledVersion, UpdateOutput = result };
        }
    }
}