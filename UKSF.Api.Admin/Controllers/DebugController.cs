﻿using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace UKSF.Api.Admin.Controllers
{
    [Route("debug")]
    public class DebugController : ControllerBase
    {
        [HttpGet("500"), Authorize]
        public void Throw500()
        {
            throw new("This is a random error");
        }
    }
}
