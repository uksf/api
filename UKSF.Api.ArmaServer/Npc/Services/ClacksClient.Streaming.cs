using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using UKSF.Api.ArmaServer.Npc.Models;
using UKSF.Api.Core;

namespace UKSF.Api.ArmaServer.Npc.Services;

// Streaming half of the clacks client. /speak_stream emits one dynamic line as a
// sequence of base64 PCM frames (~750 ms of 24 kHz mono i16 LE) over SSE. The
// broker forwards each frame to the game as it arrives; nothing is buffered here.
public partial class ClacksClient
{
    public async Task SpeakStreamAsync(string role, string text, string voiceId, Func<string, Task> onFrame)
    {
        var baseUrl = variablesService.GetVariable("CLACKS_URL")?.Item?.ToString()?.TrimEnd('/');
        if (string.IsNullOrEmpty(baseUrl))
        {
            logger.LogWarning("CLACKS_URL not configured — clacks stream call skipped");
            return;
        }

        try
        {
            using var client = httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(90);
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/speak_stream")
            {
                Content = JsonContent.Create(
                    new
                    {
                        model = ClacksCandidates.VoiceModel,
                        nodes = ClacksCandidates.VoiceNodes,
                        text,
                        voiceId
                    },
                    options: NpcBrainJson.Options
                )
            };
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning($"clacks /speak_stream returned {(int)response.StatusCode} for role '{role}'");
                return;
            }

            await using var stream = await response.Content.ReadAsStreamAsync();
            using var reader = new StreamReader(stream);
            var clock = Stopwatch.StartNew();
            long firstFrameMs = -1;
            var frames = 0;
            string line;
            while ((line = await reader.ReadLineAsync()) is not null)
            {
                if (line.StartsWith("data: ", StringComparison.Ordinal))
                {
                    if (firstFrameMs < 0) firstFrameMs = clock.ElapsedMilliseconds;
                    frames++;
                    await onFrame(line["data: ".Length..]);
                }
            }

            // Frame spread is the difference between streaming and batching: if first and
            // last land together the audio arrives as one burst and plays late.
            logger.LogInfo($"npc stream: {frames} frames, first at {firstFrameMs}ms, last at {clock.ElapsedMilliseconds}ms");
        }
        catch (Exception exception)
        {
            logger.LogError($"clacks /speak_stream call failed for role '{role}'", exception);
        }
    }
}
