﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UKSF.Api.Base;
using UKSF.Api.Teamspeak.Services;

namespace UKSF.Api.Teamspeak.Controllers {
    [Route("[controller]")]
    public class TeamspeakController : Controller {
        private readonly ITeamspeakService teamspeakService;

        public TeamspeakController(ITeamspeakService teamspeakService) => this.teamspeakService = teamspeakService;

        [HttpGet("online"), Authorize, Permissions(Permissions.CONFIRMED, Permissions.MEMBER, Permissions.DISCHARGED)]
        public IEnumerable<object> GetOnlineClients() => teamspeakService.GetFormattedClients();

        [HttpGet("shutdown"), Authorize, Permissions(Permissions.ADMIN)]
        public async Task<IActionResult> Shutdown() {
            await teamspeakService.Shutdown();
            await Task.Delay(TimeSpan.FromSeconds(3));
            return Ok();
        }
    }
}