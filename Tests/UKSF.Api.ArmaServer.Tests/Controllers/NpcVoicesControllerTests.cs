using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using UKSF.Api.ArmaServer.Controllers;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Npc.Models;
using UKSF.Api.ArmaServer.Npc.Services;
using UKSF.Api.Core;
using UKSF.Api.Core.Exceptions;
using UKSF.Api.Core.Services;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Controllers;

public class NpcVoicesControllerTests
{
    private readonly Mock<INpcVoicesContext> _context = new();
    private readonly Mock<INpcVoiceJobsContext> _jobs = new();
    private readonly Mock<INpcVoiceStore> _store = new();
    private readonly Mock<IClacksClient> _clacks = new();
    private readonly Mock<IHttpContextService> _httpContext = new();
    private readonly Mock<IUksfLogger> _logger = new();
    private readonly NpcVoicesController _sut;

    public NpcVoicesControllerTests()
    {
        _httpContext.Setup(x => x.GetUserId()).Returns("user-1");
        _store.Setup(x => x.SaveAsync(It.IsAny<string>(), It.IsAny<byte[]>())).ReturnsAsync("smuggler.wav");
        _context.Setup(x => x.Add(It.IsAny<DomainNpcVoice>())).Returns(Task.CompletedTask);
        // Dup-slug check (predicate overload) defaults to "not found".
        _context.Setup(x => x.GetSingle(It.IsAny<Func<DomainNpcVoice, bool>>())).Returns((DomainNpcVoice)null);
        _clacks.Setup(x => x.PutVoiceAsync(It.IsAny<string>(), It.IsAny<byte[]>())).ReturnsAsync(true);
        _sut = new NpcVoicesController(_context.Object, _jobs.Object, _store.Object, _clacks.Object, _httpContext.Object, _logger.Object);
    }

    private static IFormFile WavFile(int dataBytes)
    {
        var stream = new MemoryStream(BuildWav(24000, 1, 16, dataBytes));
        return new FormFile(stream, 0, stream.Length, "sample", "sample.wav") { Headers = new HeaderDictionary(), ContentType = "audio/wav" };
    }

    private static byte[] BuildWav(int sampleRate, short channels, short bits, int dataBytes)
    {
        var blockAlign = (short)(channels * bits / 8);
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        w.Write(Encoding.ASCII.GetBytes("RIFF"));
        w.Write(36 + dataBytes);
        w.Write(Encoding.ASCII.GetBytes("WAVE"));
        w.Write(Encoding.ASCII.GetBytes("fmt "));
        w.Write(16);
        w.Write((short)1);
        w.Write(channels);
        w.Write(sampleRate);
        w.Write(sampleRate * blockAlign);
        w.Write(blockAlign);
        w.Write(bits);
        w.Write(Encoding.ASCII.GetBytes("data"));
        w.Write(dataBytes);
        w.Write(new byte[dataBytes]);
        w.Flush();
        return ms.ToArray();
    }

    [Fact]
    public void Controller_requires_mission_maker_or_admin_permission()
    {
        var attr = typeof(NpcVoicesController).GetCustomAttributes(typeof(PermissionsAttribute), false).Cast<PermissionsAttribute>().Single();
        attr.Roles!.Split(',').Should().BeEquivalentTo(Permissions.Nco, Permissions.Servers, Permissions.Command, Permissions.Admin);
    }

    [Fact]
    public async Task Upload_stores_voice_and_pushes_to_clacks()
    {
        var result = await _sut.Upload(WavFile(24000 * 2 * 5), "Smuggler", null, null); // 5s clip
        result.VoiceId.Should().Be("smuggler");
        result.OwnerId.Should().Be("user-1");
        _store.Verify(x => x.SaveAsync("smuggler", It.IsAny<byte[]>()), Times.Once);
        _clacks.Verify(x => x.PutVoiceAsync("smuggler", It.IsAny<byte[]>()), Times.Once);
        _context.Verify(x => x.Add(It.Is<DomainNpcVoice>(v => v.VoiceId == "smuggler")), Times.Once);
    }

    [Fact]
    public async Task Upload_rejects_clip_shorter_than_three_seconds()
    {
        var act = () => _sut.Upload(WavFile(24000 * 2 * 1), "Smuggler", null, null); // 1s
        await act.Should().ThrowAsync<Exception>();
        _store.Verify(x => x.SaveAsync(It.IsAny<string>(), It.IsAny<byte[]>()), Times.Never);
    }

    [Fact]
    public async Task Upload_rejects_duplicate_slug()
    {
        _context.Setup(x => x.GetSingle(It.IsAny<Func<DomainNpcVoice, bool>>())).Returns(new DomainNpcVoice { VoiceId = "smuggler" });
        var act = () => _sut.Upload(WavFile(24000 * 2 * 5), "Smuggler", null, null);
        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task Mood_variant_gets_compound_slug_and_moodOf()
    {
        // Parent "smuggler" exists; the new slug "smuggler_angry" does not.
        _context.Setup(x => x.GetSingle(It.IsAny<Func<DomainNpcVoice, bool>>()))
                .Returns((Func<DomainNpcVoice, bool> p) => p(new DomainNpcVoice { VoiceId = "smuggler" }) ? new DomainNpcVoice { VoiceId = "smuggler" } : null);
        var result = await _sut.Upload(WavFile(24000 * 2 * 5), "ignored", "smuggler", "Angry");
        result.VoiceId.Should().Be("smuggler_angry");
        result.MoodOf.Should().Be("smuggler");
    }

    [Fact]
    public async Task Mood_variant_with_unknown_parent_is_rejected()
    {
        // Default GetSingle(predicate) returns null → parent not found.
        var act = () => _sut.Upload(WavFile(24000 * 2 * 5), "ignored", "ghost", "Angry");
        await act.Should().ThrowAsync<Exception>();
        _store.Verify(x => x.SaveAsync(It.IsAny<string>(), It.IsAny<byte[]>()), Times.Never);
    }

    [Fact]
    public async Task Delete_by_owner_removes_doc_and_master_file()
    {
        _context.Setup(x => x.GetSingle("v1"))
        .Returns(
            new DomainNpcVoice
            {
                Id = "v1",
                VoiceId = "smuggler",
                OwnerId = "user-1",
                FilePath = "smuggler.wav"
            }
        );
        _context.Setup(x => x.Delete("v1")).Returns(Task.CompletedTask);
        var result = await _sut.Delete("v1");
        result.Should().BeOfType<OkResult>();
        _store.Verify(x => x.Delete("smuggler.wav"), Times.Once);
        _context.Verify(x => x.Delete("v1"), Times.Once);
    }

    [Fact]
    public async Task Delete_by_non_owner_non_admin_is_forbidden()
    {
        _httpContext.Setup(x => x.GetUserId()).Returns("someone-else");
        _httpContext.Setup(x => x.UserHasPermission(Permissions.Admin)).Returns(false);
        _context.Setup(x => x.GetSingle("v1"))
        .Returns(
            new DomainNpcVoice
            {
                Id = "v1",
                VoiceId = "smuggler",
                OwnerId = "user-1",
                FilePath = "smuggler.wav"
            }
        );
        var result = await _sut.Delete("v1");
        result.Should().BeOfType<ForbidResult>();
        _store.Verify(x => x.Delete(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GenerateMoods_enqueues_a_job_with_the_four_pending_moods()
    {
        _context.Setup(x => x.GetSingle(It.IsAny<Func<DomainNpcVoice, bool>>()))
        .Returns(
            new DomainNpcVoice
            {
                VoiceId = "smuggler",
                MoodOf = null,
                OwnerId = "owner1"
            }
        );
        _httpContext.Setup(x => x.GetUserId()).Returns("owner1");
        _jobs.Setup(x => x.GetSingle(It.IsAny<Func<DomainNpcVoiceJob, bool>>())).Returns((DomainNpcVoiceJob)null);
        _jobs.Setup(x => x.Add(It.IsAny<DomainNpcVoiceJob>())).Returns(Task.CompletedTask);

        var job = await _sut.GenerateMoods("smuggler");

        job.BaseVoiceId.Should().Be("smuggler");
        job.Moods.Select(m => m.Mood).Should().BeEquivalentTo(MoodScripts.Generated);
        _jobs.Verify(x => x.Add(It.Is<DomainNpcVoiceJob>(j => j.BaseVoiceId == "smuggler")), Times.Once);
    }

    [Fact]
    public async Task GenerateMoods_rejects_an_unknown_base_voice()
    {
        _context.Setup(x => x.GetSingle(It.IsAny<Func<DomainNpcVoice, bool>>())).Returns((DomainNpcVoice)null);
        var act = async () => await _sut.GenerateMoods("ghost");
        await act.Should().ThrowAsync<BadRequestException>();
    }

    [Fact]
    public async Task GenerateMoods_rejects_a_mood_variant_as_base()
    {
        _context.Setup(x => x.GetSingle(It.IsAny<Func<DomainNpcVoice, bool>>()))
                .Returns(new DomainNpcVoice { VoiceId = "smuggler_angry", MoodOf = "smuggler" });
        var act = async () => await _sut.GenerateMoods("smuggler_angry");
        await act.Should().ThrowAsync<BadRequestException>();
    }
}
