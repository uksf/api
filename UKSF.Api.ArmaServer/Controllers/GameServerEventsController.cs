using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.ArmaServer.Parsing;
using UKSF.Api.ArmaServer.Services;
using UKSF.Api.Core;
using static UKSF.Api.ArmaServer.Converters.PersistenceConversionHelpers;

namespace UKSF.Api.ArmaServer.Controllers;

[Route("gameservers/events")]
[AllowAnonymous]
[LocalhostOnly]
public class GameServerEventsController(IGameServerEventHandler eventHandler, IServiceScopeFactory scopeFactory, IUksfLogger logger) : ControllerBase
{
    [HttpPost]
    [DisableRequestSizeLimit]
    public async Task<IActionResult> ReceiveEvent()
    {
        using var reader = new StreamReader(Request.Body);
        var body = await reader.ReadToEndAsync();
        if (string.IsNullOrWhiteSpace(body))
        {
            return BadRequest("empty body");
        }

        if (!int.TryParse(Request.Headers["X-Api-Port"].ToString(), out var apiPort))
        {
            return BadRequest("missing X-Api-Port header");
        }

        var enqueueAt = Request.Headers["X-Enqueued-At"].ToString();

        Dictionary<string, object> data;
        string type;
        try
        {
            var parsed = SqfNotationParser.ParseAndNormalize(body);
            if (parsed is not List<object> envelope || envelope.Count != 2 || envelope[0] is not string parsedType)
            {
                return BadRequest("expected [type, data] envelope");
            }

            type = parsedType;
            data = ToDict(envelope[1]);
        }
        catch (FormatException exception)
        {
            logger.LogError($"Failed to parse SQF event body: {body[..Math.Min(body.Length, 200)]}", exception);
            return BadRequest("malformed SQF body");
        }

        if (!string.IsNullOrEmpty(enqueueAt))
        {
            if (data.ContainsKey("enqueueAt"))
            {
                logger.LogWarning($"SQF payload already contains 'enqueueAt' for type '{type}'; overwriting with header value");
            }

            data["enqueueAt"] = enqueueAt;
        }

        var gameServerEvent = new GameServerEvent
        {
            Type = type,
            ApiPort = apiPort,
            Data = data
        };

        if (type == "persistence_save")
        {
            try
            {
                await eventHandler.HandleEventAsync(gameServerEvent);
            }
            catch (PersistenceSaveRejectedException exception) when (Request.Headers["X-Uksf-Protocol"] == "2")
            {
                return UnprocessableEntity(exception.Message);
            }
            catch (PersistenceSaveRejectedException)
            {
                return Ok();
            }

            return Ok();
        }

        _ = Task.Run(async () =>
            {
                try
                {
                    await using var scope = scopeFactory.CreateAsyncScope();
                    var handler = scope.ServiceProvider.GetRequiredService<IGameServerEventHandler>();
                    await handler.HandleEventAsync(gameServerEvent);
                }
                catch (Exception exception)
                {
                    logger.LogError($"Background handling of game server event '{type}' failed", exception);
                }
            }
        );

        return Accepted();
    }
}
