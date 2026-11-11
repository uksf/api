using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Models.Domain;
using Xunit;

namespace UKSF.Api.Integrations.Teamspeak.Tests.Services;

public class TeamspeakGroupServiceCheckClientTests : TeamspeakGroupServiceTestBase
{
    private void SetupAccounts(params DomainAccount[] accounts)
    {
        _mockAccountContext.Setup(x => x.Get(It.IsAny<Func<DomainAccount, bool>>())).Returns<Func<DomainAccount, bool>>(predicate => accounts.Where(predicate));
    }

    [Fact]
    public async Task Does_nothing_for_a_client_with_no_account()
    {
        SetupAccounts(new DomainAccount { TeamspeakIdentities = [5] });

        await _teamspeakGroupService.CheckClientGroups(70, [6]);

        _addedGroups.Should().BeEmpty();
        _removedGroups.Should().BeEmpty();
    }

    [Fact]
    public async Task Uses_the_highest_membership_account_for_the_client()
    {
        SetupAccounts(
            new DomainAccount { MembershipState = MembershipState.Discharged, TeamspeakIdentities = [70] },
            new DomainAccount { MembershipState = MembershipState.Unconfirmed, TeamspeakIdentities = [70] }
        );

        await _teamspeakGroupService.CheckClientGroups(70, new List<int> { 1 });

        _addedGroups.Should().BeEquivalentTo([2]);
        _removedGroups.Should().BeEquivalentTo([1]);
    }
}
