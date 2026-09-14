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
    // Loopback-only endpoint receiving SQF event bodies from the extension.
    // persistence_save payloads can grow into the tens-of-MB range for large
    // sessions; default Kestrel cap (30MB) would 413 silently before the
    // controller is reached and the extension's fire-and-forget retry would
    // eventually drop the event. Lift the cap to keep large saves intact.
    [HttpPost]
    [DisableRequestSizeLimit]
    public async Task<IActionResult> ReceiveEvent()
    {
        // Wire format is engine-native SQF str() output: `[<type>,<data>]`.
        // Extension forwards verbatim and stamps the X-Api-Port / X-Enqueued-At
        // headers — both are inside the extension's knowledge, not the game's.
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

        // Inject enqueueAt into data so existing handler logic continues to read it from there.
        // The header is the source of truth (extension stamps at queue time); warn loudly if the
        // game side ever sends its own value so a contract drift doesn't slip through silently.
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

        // persistence_save must finish before the ack so a failed write is retried by the
        // extension. Everything else (npc_turn included) acks immediately — the extension's
        // HTTP client times out at 5s, and a brain+TTS turn routinely exceeds that, which
        // caused retries, connection storms, and silent drops.
        if (type == "persistence_save")
        {
            await eventHandler.HandleEventAsync(gameServerEvent);
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
