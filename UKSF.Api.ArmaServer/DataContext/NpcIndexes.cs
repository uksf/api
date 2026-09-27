using Microsoft.Extensions.Hosting;
using MongoDB.Bson;
using MongoDB.Driver;
using UKSF.Api.ArmaServer.Npc.Models;
using UKSF.Api.ArmaServer.Npc.Observability;
using UKSF.Api.Core;

namespace UKSF.Api.ArmaServer.DataContext;

public class NpcIndexes(IMongoDatabase database, IUksfLogger logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            var sessions = database.GetCollection<DomainNpcSession>("npcSessions");
            await sessions.Indexes.CreateOneAsync(
                new CreateIndexModel<DomainNpcSession>(
                    Builders<DomainNpcSession>.IndexKeys.Ascending(x => x.SessionId).Ascending(x => x.NpcId),
                    new CreateIndexOptions { Name = "ix_sessionId_npcId", Unique = true }
                ),
                cancellationToken: cancellationToken
            );

            // Self-heal: drop sessions whose mission_ended cleanup never arrived.
            await sessions.Indexes.CreateOneAsync(
                new CreateIndexModel<DomainNpcSession>(
                    Builders<DomainNpcSession>.IndexKeys.Ascending(x => x.CreatedAt),
                    new CreateIndexOptions { Name = "ix_ttl_createdAt", ExpireAfter = TimeSpan.FromHours(24) }
                ),
                cancellationToken: cancellationToken
            );

            var clips = database.GetCollection<DomainNpcAudioClip>("npcAudioClips");
            await clips.Indexes.CreateOneAsync(
                new CreateIndexModel<DomainNpcAudioClip>(
                    Builders<DomainNpcAudioClip>.IndexKeys.Ascending(x => x.SessionId).Ascending(x => x.NpcId).Ascending(x => x.ClipId),
                    new CreateIndexOptions { Name = "ix_sessionId_npcId_clipId", Unique = true }
                ),
                cancellationToken: cancellationToken
            );

            var voices = database.GetCollection<DomainNpcVoice>("npcVoices");
            await voices.Indexes.CreateOneAsync(
                new CreateIndexModel<DomainNpcVoice>(
                    Builders<DomainNpcVoice>.IndexKeys.Ascending(x => x.VoiceId),
                    new CreateIndexOptions { Name = "ix_voiceId", Unique = true }
                ),
                cancellationToken: cancellationToken
            );

            var traces = database.GetCollection<BsonDocument>(MongoNpcTraceSink.Collection);
            await traces.Indexes.CreateManyAsync(
                [
                    new CreateIndexModel<BsonDocument>(
                        Builders<BsonDocument>.IndexKeys.Ascending("session").Ascending("proc").Ascending("seq"),
                        new CreateIndexOptions { Name = "ix_session_proc_seq" }
                    ),
                    new CreateIndexModel<BsonDocument>(
                        Builders<BsonDocument>.IndexKeys.Ascending("turn"),
                        new CreateIndexOptions { Name = "ix_turn", Sparse = true }
                    ),
                    new CreateIndexModel<BsonDocument>(
                        Builders<BsonDocument>.IndexKeys.Ascending("type").Ascending("at"),
                        new CreateIndexOptions { Name = "ix_type_at" }
                    ),
                    new CreateIndexModel<BsonDocument>(
                        Builders<BsonDocument>.IndexKeys.Ascending("data.uid").Ascending("at"),
                        new CreateIndexOptions { Name = "ix_uid_at", Sparse = true }
                    )
                ],
                cancellationToken
            );

            var jobs = database.GetCollection<DomainNpcVoiceJob>("npcVoiceJobs");
            await jobs.Indexes.CreateOneAsync(
                new CreateIndexModel<DomainNpcVoiceJob>(
                    Builders<DomainNpcVoiceJob>.IndexKeys.Ascending(x => x.BaseVoiceId),
                    new CreateIndexOptions { Name = "ix_baseVoiceId", Unique = true }
                ),
                cancellationToken: cancellationToken
            );
        }
        catch (Exception exception)
        {
            logger.LogError("Failed to create NPC indexes", exception);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
