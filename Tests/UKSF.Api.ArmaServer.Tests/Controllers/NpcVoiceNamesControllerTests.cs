using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Moq;
using UKSF.Api.ArmaServer.Controllers;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Npc.Models;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Controllers;

public class NpcVoiceNamesControllerTests
{
    [Fact]
    public void Lists_base_voices_by_name_without_moods()
    {
        var context = new Mock<INpcVoicesContext>();
        context.Setup(x => x.Get())
               .Returns(
                   new List<DomainNpcVoice>
                   {
                       new() { VoiceId = "tomas", DisplayName = "Tomas" },
                       new()
                       {
                           VoiceId = "merl_angry",
                           DisplayName = "Merl (angry)",
                           MoodOf = "merl"
                       },
                       new() { VoiceId = "merl", DisplayName = "merl" }
                   }
               );

        var result = new NpcVoiceNamesController(context.Object).GetNames();

        result.Should().Equal(new NpcVoiceName("merl", "merl"), new NpcVoiceName("tomas", "Tomas"));
    }

    [Fact]
    public void Needs_no_login()
    {
        typeof(NpcVoiceNamesController).GetMethod(nameof(NpcVoiceNamesController.GetNames))!.GetCustomAttribute<AllowAnonymousAttribute>().Should().NotBeNull();
        typeof(NpcVoiceNamesController).GetCustomAttributes<AuthorizeAttribute>().Should().BeEmpty();
    }
}
