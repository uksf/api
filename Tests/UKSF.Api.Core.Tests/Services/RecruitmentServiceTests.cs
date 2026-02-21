using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Mappers;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;
using Xunit;

namespace UKSF.Api.Core.Tests.Services;

public class RecruitmentServiceTests
{
    private const string RecruiterUnitId = "recruiter-unit-id";

    private readonly Mock<IAccountContext> _mockAccountContext = new();
    private readonly Mock<IUnitsContext> _mockUnitsContext = new();
    private readonly Mock<IHttpContextService> _mockHttpContextService = new();
    private readonly Mock<IDisplayNameService> _mockDisplayNameService = new();
    private readonly Mock<IRanksService> _mockRanksService = new();
    private readonly Mock<IVariablesService> _mockVariablesService = new();
    private readonly Mock<IAccountMapper> _mockAccountMapper = new();
    private readonly RecruitmentService _subject;

    public RecruitmentServiceTests()
    {
        _mockVariablesService.Setup(x => x.GetVariable("UNIT_ID_RECRUITMENT"))
                             .Returns(new DomainVariableItem { Key = "UNIT_ID_RECRUITMENT", Item = RecruiterUnitId });

        _subject = new RecruitmentService(
            _mockAccountContext.Object,
            _mockUnitsContext.Object,
            _mockHttpContextService.Object,
            _mockDisplayNameService.Object,
            _mockRanksService.Object,
            _mockVariablesService.Object,
            _mockAccountMapper.Object
        );
    }

    private static DomainAccount CreateAccount(string id, string firstname = "John", string lastname = "Doe", string rank = "Private", bool sr1Enabled = true)
    {
        return new DomainAccount
        {
            Id = id,
            Firstname = firstname,
            Lastname = lastname,
            Rank = rank,
            Settings = new AccountSettings { Sr1Enabled = sr1Enabled }
        };
    }

    private void SetupRecruiterUnit(List<string> members, ChainOfCommand chainOfCommand = null)
    {
        _mockUnitsContext.Setup(x => x.GetSingle(RecruiterUnitId))
        .Returns(
            new DomainUnit
            {
                Id = RecruiterUnitId,
                Members = members,
                ChainOfCommand = chainOfCommand ?? new ChainOfCommand()
            }
        );
    }

    private void SetupAccountLookup(params DomainAccount[] accounts)
    {
        foreach (var account in accounts)
        {
            _mockAccountContext.Setup(x => x.GetSingle(account.Id)).Returns(account);
        }
    }

    #region GetRecruiterAccounts

    [Fact]
    public void GetRecruiterAccounts_ReturnsMemberAccounts()
    {
        var account1 = CreateAccount("id1", "Alice", "Smith", "Private");
        var account2 = CreateAccount("id2", "Bob", "Jones", "Corporal");
        SetupRecruiterUnit(["id1", "id2"]);
        SetupAccountLookup(account1, account2);

        var result = _subject.GetRecruiterAccounts().ToList();

        result.Should().HaveCount(2);
        result.Select(x => x.Id).Should().Contain("id1").And.Contain("id2");
    }

    [Fact]
    public void GetRecruiterAccounts_SortedByRankThenLastnameThenFirstname()
    {
        var account1 = CreateAccount("id1", "Charlie", "Beta", "Private");
        var account2 = CreateAccount("id2", "Alice", "Beta", "Private");
        var account3 = CreateAccount("id3", "Bob", "Alpha", "Private");
        SetupRecruiterUnit(["id1", "id2", "id3"]);
        SetupAccountLookup(account1, account2, account3);

        _mockRanksService.Setup(x => x.IsSuperior(It.IsAny<string>(), It.IsAny<string>())).Returns(false);

        var result = _subject.GetRecruiterAccounts(skipSortByRank: false).ToList();

        // All same rank, so sorted by Lastname then Firstname
        result[0].Lastname.Should().Be("Alpha");
        result[1].Firstname.Should().Be("Alice");
        result[2].Firstname.Should().Be("Charlie");
    }

    [Fact]
    public void GetRecruiterAccounts_SkipSortByRank_ReturnsUnsorted()
    {
        var account1 = CreateAccount("id1", "Charlie", "Zulu", "Private");
        var account2 = CreateAccount("id2", "Alice", "Alpha", "Corporal");
        SetupRecruiterUnit(["id1", "id2"]);
        SetupAccountLookup(account1, account2);

        var result = _subject.GetRecruiterAccounts(skipSortByRank: true).ToList();

        // Should preserve original member order from unit
        result[0].Id.Should().Be("id1");
        result[1].Id.Should().Be("id2");
    }

    #endregion

    #region GetRecruiterLeadAccountIds

    [Fact]
    public void GetRecruiterLeadAccountIds_AllPositionsFilled_ReturnsFourIds()
    {
        var chainOfCommand = new ChainOfCommand
        {
            First = "lead1",
            Second = "lead2",
            Third = "lead3",
            Nco = "lead4"
        };
        SetupRecruiterUnit([], chainOfCommand);

        var result = _subject.GetRecruiterLeadAccountIds();

        result.Should().BeEquivalentTo(["lead1", "lead2", "lead3", "lead4"]);
    }

    [Fact]
    public void GetRecruiterLeadAccountIds_SomePositionsEmpty_ReturnsOnlyFilledPositions()
    {
        var chainOfCommand = new ChainOfCommand
        {
            First = "lead1",
            Second = "",
            Third = null,
            Nco = "lead4"
        };
        SetupRecruiterUnit([], chainOfCommand);

        var result = _subject.GetRecruiterLeadAccountIds();

        result.Should().BeEquivalentTo(["lead1", "lead4"]);
    }

    [Fact]
    public void GetRecruiterLeadAccountIds_NoChainOfCommand_ReturnsEmptyList()
    {
        SetupRecruiterUnit([]);

        var result = _subject.GetRecruiterLeadAccountIds();

        result.Should().BeEmpty();
    }

    #endregion

    #region IsRecruiter

    [Fact]
    public void IsRecruiter_AccountIsInRecruiterUnit_ReturnsTrue()
    {
        var account = CreateAccount("id1");
        SetupRecruiterUnit(["id1"]);
        SetupAccountLookup(account);

        var result = _subject.IsRecruiter(account);

        result.Should().BeTrue();
    }

    [Fact]
    public void IsRecruiter_AccountIsNotInRecruiterUnit_ReturnsFalse()
    {
        var account = CreateAccount("id1");
        var otherAccount = CreateAccount("id2");
        SetupRecruiterUnit(["id2"]);
        SetupAccountLookup(otherAccount);

        var result = _subject.IsRecruiter(account);

        result.Should().BeFalse();
    }

    #endregion

    #region IsRecruiterLead

    [Fact]
    public void IsRecruiterLead_AccountProvidedAndIsLead_ReturnsTrue()
    {
        var account = CreateAccount("lead1");
        var chainOfCommand = new ChainOfCommand { First = "lead1" };
        SetupRecruiterUnit([], chainOfCommand);

        var result = _subject.IsRecruiterLead(account);

        result.Should().BeTrue();
    }

    [Fact]
    public void IsRecruiterLead_AccountProvidedAndIsNotLead_ReturnsFalse()
    {
        var account = CreateAccount("not-a-lead");
        var chainOfCommand = new ChainOfCommand { First = "lead1" };
        SetupRecruiterUnit([], chainOfCommand);

        var result = _subject.IsRecruiterLead(account);

        result.Should().BeFalse();
    }

    [Fact]
    public void IsRecruiterLead_AccountNull_UsesHttpContextUserId()
    {
        _mockHttpContextService.Setup(x => x.GetUserId()).Returns("lead1");
        var chainOfCommand = new ChainOfCommand { First = "lead1" };
        SetupRecruiterUnit([], chainOfCommand);

        var result = _subject.IsRecruiterLead();

        result.Should().BeTrue();
        _mockHttpContextService.Verify(x => x.GetUserId(), Times.Once);
    }

    [Fact]
    public void IsRecruiterLead_AccountNull_UserIdNotInLeads_ReturnsFalse()
    {
        _mockHttpContextService.Setup(x => x.GetUserId()).Returns("not-a-lead");
        var chainOfCommand = new ChainOfCommand { First = "lead1" };
        SetupRecruiterUnit([], chainOfCommand);

        var result = _subject.IsRecruiterLead();

        result.Should().BeFalse();
    }

    #endregion

    #region GetNextRecruiterForApplication

    [Fact]
    public void GetNextRecruiterForApplication_SelectsRecruiterWithFewestWaiting()
    {
        var recruiter1 = CreateAccount("r1", sr1Enabled: true);
        var recruiter2 = CreateAccount("r2", sr1Enabled: true);
        SetupRecruiterUnit(["r1", "r2"]);
        SetupAccountLookup(recruiter1, recruiter2);

        // r1 has 2 waiting, r2 has 0 waiting
        var waitingApps = new List<DomainAccount>
        {
            new() { Id = "app1", Application = new DomainApplication { State = ApplicationState.Waiting, Recruiter = "r1" } },
            new() { Id = "app2", Application = new DomainApplication { State = ApplicationState.Waiting, Recruiter = "r1" } }
        };
        var completeApps = new List<DomainAccount>();

        _mockAccountContext.Setup(x => x.Get(It.Is<Func<DomainAccount, bool>>(f => f(waitingApps[0])))).Returns(waitingApps);
        _mockAccountContext.Setup(x => x.Get(
                                      It.Is<Func<DomainAccount, bool>>(f => !f(waitingApps[0]) &&
                                                                            f(
                                                                                new DomainAccount
                                                                                {
                                                                                    Application = new DomainApplication
                                                                                    {
                                                                                        State = ApplicationState.Accepted
                                                                                    }
                                                                                }
                                                                            )
                                      )
                                  )
                           )
                           .Returns(completeApps);

        var result = _subject.GetNextRecruiterForApplication();

        result.Should().Be("r2");
    }

    [Fact]
    public void GetNextRecruiterForApplication_TieInWaiting_BreaksByFewestComplete()
    {
        var recruiter1 = CreateAccount("r1", sr1Enabled: true);
        var recruiter2 = CreateAccount("r2", sr1Enabled: true);
        SetupRecruiterUnit(["r1", "r2"]);
        SetupAccountLookup(recruiter1, recruiter2);

        // Both have 0 waiting, but r1 has 2 complete, r2 has 1 complete
        var waitingApps = new List<DomainAccount>();
        var completeApps = new List<DomainAccount>
        {
            new() { Id = "c1", Application = new DomainApplication { State = ApplicationState.Accepted, Recruiter = "r1" } },
            new() { Id = "c2", Application = new DomainApplication { State = ApplicationState.Accepted, Recruiter = "r1" } },
            new() { Id = "c3", Application = new DomainApplication { State = ApplicationState.Accepted, Recruiter = "r2" } }
        };

        _mockAccountContext.Setup(x => x.Get(
                                      It.Is<Func<DomainAccount, bool>>(f => f(
                                                                           new DomainAccount
                                                                           {
                                                                               Application = new DomainApplication { State = ApplicationState.Waiting }
                                                                           }
                                                                       )
                                      )
                                  )
                           )
                           .Returns(waitingApps);
        _mockAccountContext.Setup(x => x.Get(
                                      It.Is<Func<DomainAccount, bool>>(f => f(
                                                                           new DomainAccount
                                                                           {
                                                                               Application = new DomainApplication { State = ApplicationState.Accepted }
                                                                           }
                                                                       )
                                      )
                                  )
                           )
                           .Returns(completeApps);

        var result = _subject.GetNextRecruiterForApplication();

        result.Should().Be("r2");
    }

    [Fact]
    public void GetNextRecruiterForApplication_NoSr1EnabledRecruiters_ReturnsEmptyString()
    {
        var recruiter1 = CreateAccount("r1", sr1Enabled: false);
        var recruiter2 = CreateAccount("r2", sr1Enabled: false);
        SetupRecruiterUnit(["r1", "r2"]);
        SetupAccountLookup(recruiter1, recruiter2);

        _mockAccountContext.Setup(x => x.Get(It.IsAny<Func<DomainAccount, bool>>())).Returns(new List<DomainAccount>());

        var result = _subject.GetNextRecruiterForApplication();

        result.Should().BeEmpty();
    }

    [Fact]
    public void GetNextRecruiterForApplication_NoRecruiters_ReturnsEmptyString()
    {
        SetupRecruiterUnit([]);

        _mockAccountContext.Setup(x => x.Get(It.IsAny<Func<DomainAccount, bool>>())).Returns(new List<DomainAccount>());

        var result = _subject.GetNextRecruiterForApplication();

        result.Should().BeEmpty();
    }

    #endregion

    #region SetApplicationRecruiter

    [Fact]
    public async Task SetApplicationRecruiter_UpdatesAccountApplicationRecruiter()
    {
        var accountId = "account-id";
        var newRecruiter = "new-recruiter-id";

        await _subject.SetApplicationRecruiter(accountId, newRecruiter);

        _mockAccountContext.Verify(x => x.Update(accountId, It.IsAny<MongoDB.Driver.UpdateDefinition<DomainAccount>>()), Times.Once);
    }

    #endregion

    #region GetStats

    [Fact]
    public void GetStats_AllAccounts_ReturnsCorrectStatistics()
    {
        var now = DateTime.UtcNow;
        var accounts = new List<DomainAccount>
        {
            new()
            {
                Id = "a1",
                Application = new DomainApplication
                {
                    State = ApplicationState.Accepted,
                    Recruiter = "r1",
                    DateCreated = now.AddDays(-10),
                    DateAccepted = now.AddDays(-5)
                }
            },
            new()
            {
                Id = "a2",
                Application = new DomainApplication
                {
                    State = ApplicationState.Rejected,
                    Recruiter = "r1",
                    DateCreated = now.AddDays(-20),
                    DateAccepted = now.AddDays(-12)
                }
            },
            new()
            {
                Id = "a3",
                Application = new DomainApplication
                {
                    State = ApplicationState.Waiting,
                    Recruiter = "r1",
                    DateCreated = now.AddDays(-3)
                }
            }
        };

        _mockAccountContext.Setup(x => x.Get(It.IsAny<Func<DomainAccount, bool>>()))
                           .Returns((Func<DomainAccount, bool> predicate) => accounts.Where(predicate));

        var result = _subject.GetStats(string.Empty, false).ToList();

        result.Should().HaveCount(5);
        result.First(x => x.FieldName == "Accepted applications").FieldValue.Should().Be("1");
        result.First(x => x.FieldName == "Rejected applications").FieldValue.Should().Be("1");
        result.First(x => x.FieldName == "Waiting applications").FieldValue.Should().Be("1");
        result.First(x => x.FieldName == "Enlistment Rate").FieldValue.Should().Be("50%");
    }

    [Fact]
    public void GetStats_FilteredByAccount_ReturnsOnlyThatRecruitersStats()
    {
        var now = DateTime.UtcNow;
        var accounts = new List<DomainAccount>
        {
            new()
            {
                Id = "a1",
                Application = new DomainApplication
                {
                    State = ApplicationState.Accepted,
                    Recruiter = "r1",
                    DateCreated = now.AddDays(-10),
                    DateAccepted = now.AddDays(-5)
                }
            },
            new()
            {
                Id = "a2",
                Application = new DomainApplication
                {
                    State = ApplicationState.Accepted,
                    Recruiter = "r2",
                    DateCreated = now.AddDays(-8),
                    DateAccepted = now.AddDays(-3)
                }
            }
        };

        _mockAccountContext.Setup(x => x.Get(It.IsAny<Func<DomainAccount, bool>>()))
                           .Returns((Func<DomainAccount, bool> predicate) => accounts.Where(predicate));

        var result = _subject.GetStats("r1", false).ToList();

        result.First(x => x.FieldName == "Accepted applications").FieldValue.Should().Be("1");
    }

    [Fact]
    public void GetStats_MonthlyFilter_OnlyIncludesRecentApplications()
    {
        var now = DateTime.UtcNow;
        var accounts = new List<DomainAccount>
        {
            new()
            {
                Id = "a1",
                Application = new DomainApplication
                {
                    State = ApplicationState.Accepted,
                    Recruiter = "r1",
                    DateCreated = now.AddDays(-10),
                    DateAccepted = now.AddDays(-5)
                }
            },
            new()
            {
                Id = "a2",
                Application = new DomainApplication
                {
                    State = ApplicationState.Accepted,
                    Recruiter = "r1",
                    DateCreated = now.AddDays(-90),
                    DateAccepted = now.AddDays(-60)
                }
            }
        };

        _mockAccountContext.Setup(x => x.Get(It.IsAny<Func<DomainAccount, bool>>()))
                           .Returns((Func<DomainAccount, bool> predicate) => accounts.Where(predicate));

        var result = _subject.GetStats(string.Empty, true).ToList();

        result.First(x => x.FieldName == "Accepted applications").FieldValue.Should().Be("1");
    }

    [Fact]
    public void GetStats_NoProcessedApplications_ReturnsZeroAverageAndZeroEnlistmentRate()
    {
        var now = DateTime.UtcNow;
        var accounts = new List<DomainAccount>
        {
            new()
            {
                Id = "a1",
                Application = new DomainApplication
                {
                    State = ApplicationState.Waiting,
                    Recruiter = "r1",
                    DateCreated = now.AddDays(-5)
                }
            }
        };

        _mockAccountContext.Setup(x => x.Get(It.IsAny<Func<DomainAccount, bool>>()))
                           .Returns((Func<DomainAccount, bool> predicate) => accounts.Where(predicate));

        var result = _subject.GetStats(string.Empty, false).ToList();

        result.First(x => x.FieldName == "Average processing time").FieldValue.Should().Be("0 Days");
        result.First(x => x.FieldName == "Enlistment Rate").FieldValue.Should().Be("0%");
        result.First(x => x.FieldName == "Waiting applications").FieldValue.Should().Be("1");
    }

    [Fact]
    public void GetStats_AverageProcessingTime_CalculatedCorrectly()
    {
        var now = DateTime.UtcNow;
        var accounts = new List<DomainAccount>
        {
            new()
            {
                Id = "a1",
                Application = new DomainApplication
                {
                    State = ApplicationState.Accepted,
                    Recruiter = "r1",
                    DateCreated = now.AddDays(-10),
                    DateAccepted = now
                }
            },
            new()
            {
                Id = "a2",
                Application = new DomainApplication
                {
                    State = ApplicationState.Rejected,
                    Recruiter = "r1",
                    DateCreated = now.AddDays(-20),
                    DateAccepted = now
                }
            }
        };

        _mockAccountContext.Setup(x => x.Get(It.IsAny<Func<DomainAccount, bool>>()))
                           .Returns((Func<DomainAccount, bool> predicate) => accounts.Where(predicate));

        var result = _subject.GetStats(string.Empty, false).ToList();

        // (10 + 20) / 2 = 15
        result.First(x => x.FieldName == "Average processing time").FieldValue.Should().Be("15 Days");
    }

    #endregion

    #region GetActiveApplications

    [Fact]
    public void GetActiveApplications_ReturnsMappedActiveApplications()
    {
        var now = DateTime.UtcNow;
        var waitingAccount = new DomainAccount
        {
            Id = "a1",
            Steamname = "steam123",
            Application = new DomainApplication
            {
                State = ApplicationState.Waiting,
                Recruiter = "r1",
                DateCreated = now.AddDays(-5)
            }
        };
        var mappedAccount = new Account { Id = "a1" };

        // GetActiveApplications calls Get with waiting filter
        _mockAccountContext.Setup(x => x.Get(It.IsAny<Func<DomainAccount, bool>>()))
                           .Returns((Func<DomainAccount, bool> predicate) =>
                               {
                                   var allAccounts = new List<DomainAccount> { waitingAccount };
                                   return allAccounts.Where(predicate);
                               }
                           );

        _mockAccountMapper.Setup(x => x.MapToAccount(waitingAccount)).Returns(mappedAccount);
        _mockDisplayNameService.Setup(x => x.GetDisplayName("r1")).Returns("Recruiter One");

        var result = _subject.GetActiveApplications();

        result.Should().HaveCount(1);
        result[0].Account.Should().Be(mappedAccount);
        result[0].SteamProfile.Should().Be("https://steamcommunity.com/profiles/steam123");
        result[0].DaysProcessing.Should().BeGreaterThanOrEqualTo(5);
        result[0].Recruiter.Should().Be("Recruiter One");
    }

    [Fact]
    public void GetActiveApplications_NoWaitingApplications_ReturnsEmptyList()
    {
        _mockAccountContext.Setup(x => x.Get(It.IsAny<Func<DomainAccount, bool>>())).Returns(new List<DomainAccount>());

        var result = _subject.GetActiveApplications();

        result.Should().BeEmpty();
    }

    [Fact]
    public void GetActiveApplications_ProcessingDifference_IsRelativeToAverage()
    {
        // Use .Date to get clean day boundary, avoiding sub-day rounding from Math.Ceiling
        var now = DateTime.UtcNow.Date;
        var waitingAccount = new DomainAccount
        {
            Id = "a1",
            Steamname = "steam1",
            Application = new DomainApplication
            {
                State = ApplicationState.Waiting,
                Recruiter = "r1",
                DateCreated = now.AddDays(-10)
            }
        };
        var completedAccount = new DomainAccount
        {
            Id = "a2",
            Application = new DomainApplication
            {
                State = ApplicationState.Accepted,
                Recruiter = "r1",
                DateCreated = now.AddDays(-20),
                DateAccepted = now.AddDays(-10)
            }
        };

        _mockAccountContext.Setup(x => x.Get(It.IsAny<Func<DomainAccount, bool>>()))
                           .Returns((Func<DomainAccount, bool> predicate) =>
                               {
                                   var allAccounts = new List<DomainAccount> { waitingAccount, completedAccount };
                                   return allAccounts.Where(predicate);
                               }
                           );

        _mockAccountMapper.Setup(x => x.MapToAccount(waitingAccount)).Returns(new Account { Id = "a1" });
        _mockDisplayNameService.Setup(x => x.GetDisplayName("r1")).Returns("Recruiter");

        var result = _subject.GetActiveApplications();

        result.Should().HaveCount(1);
        // Average processing time = 10 days (from completed account)
        // Days processing for waiting >= 10 days (Math.Ceiling adds fractional day from elapsed time)
        // Difference should be small (0 or 1 depending on time of day)
        result[0].ProcessingDifference.Should().BeGreaterThanOrEqualTo(0).And.BeLessThanOrEqualTo(1);
    }

    #endregion

    #region GetApplication

    [Fact]
    public void GetApplication_ReturnsDetailedApplicationWithRecruiterInfo()
    {
        var now = DateTime.UtcNow;
        var recruiterAccount = CreateAccount("r1", "Jane", "Recruiter", "Sergeant");
        var account = new DomainAccount
        {
            Id = "a1",
            Dob = now.AddYears(-25),
            Steamname = "steamuser",
            Application = new DomainApplication
            {
                State = ApplicationState.Waiting,
                Recruiter = "r1",
                DateCreated = now.AddDays(-7),
                DateAccepted = DateTime.MinValue
            }
        };
        var mappedAccount = new Account { Id = "a1" };

        _mockAccountContext.Setup(x => x.GetSingle("r1")).Returns(recruiterAccount);
        _mockAccountContext.Setup(x => x.Get(It.IsAny<Func<DomainAccount, bool>>())).Returns(new List<DomainAccount>());
        _mockAccountMapper.Setup(x => x.MapToAccount(account)).Returns(mappedAccount);
        _mockDisplayNameService.Setup(x => x.GetDisplayName(account)).Returns("Applicant Name");
        _mockDisplayNameService.Setup(x => x.GetDisplayName(recruiterAccount)).Returns("Jane Recruiter");
        _mockVariablesService.Setup(x => x.GetVariable("RECRUITMENT_ENTRY_AGE")).Returns(new DomainVariableItem { Key = "RECRUITMENT_ENTRY_AGE", Item = "16" });

        var result = _subject.GetApplication(account);

        result.Account.Should().Be(mappedAccount);
        result.DisplayName.Should().Be("Applicant Name");
        result.AcceptableAge.Should().Be(16);
        result.SteamProfile.Should().Be("https://steamcommunity.com/profiles/steamuser");
        result.Recruiter.Should().Be("Jane Recruiter");
        result.RecruiterId.Should().Be("r1");
        result.DaysProcessing.Should().BeGreaterThanOrEqualTo(7);
        result.Age.Should().NotBeNull();
    }

    [Fact]
    public void GetApplication_RecruiterNotFound_ReturnsEmptyRecruiterString()
    {
        var now = DateTime.UtcNow;
        var account = new DomainAccount
        {
            Id = "a1",
            Dob = now.AddYears(-20),
            Steamname = "steamuser",
            Application = new DomainApplication
            {
                State = ApplicationState.Waiting,
                Recruiter = "nonexistent",
                DateCreated = now.AddDays(-3),
                DateAccepted = DateTime.MinValue
            }
        };
        var mappedAccount = new Account { Id = "a1" };

        _mockAccountContext.Setup(x => x.GetSingle("nonexistent")).Returns((DomainAccount)null);
        _mockAccountContext.Setup(x => x.Get(It.IsAny<Func<DomainAccount, bool>>())).Returns(new List<DomainAccount>());
        _mockAccountMapper.Setup(x => x.MapToAccount(account)).Returns(mappedAccount);
        _mockDisplayNameService.Setup(x => x.GetDisplayName(account)).Returns("Display Name");
        _mockVariablesService.Setup(x => x.GetVariable("RECRUITMENT_ENTRY_AGE")).Returns(new DomainVariableItem { Key = "RECRUITMENT_ENTRY_AGE", Item = "16" });

        var result = _subject.GetApplication(account);

        result.Recruiter.Should().BeEmpty();
        result.RecruiterId.Should().BeEmpty();
    }

    [Fact]
    public void GetApplication_NextCandidateOp_IsNotEmpty()
    {
        var now = DateTime.UtcNow;
        var recruiterAccount = CreateAccount("r1");
        var account = new DomainAccount
        {
            Id = "a1",
            Dob = now.AddYears(-22),
            Steamname = "steam",
            Application = new DomainApplication
            {
                State = ApplicationState.Waiting,
                Recruiter = "r1",
                DateCreated = now.AddDays(-1),
                DateAccepted = DateTime.MinValue
            }
        };

        _mockAccountContext.Setup(x => x.GetSingle("r1")).Returns(recruiterAccount);
        _mockAccountContext.Setup(x => x.Get(It.IsAny<Func<DomainAccount, bool>>())).Returns(new List<DomainAccount>());
        _mockAccountMapper.Setup(x => x.MapToAccount(account)).Returns(new Account { Id = "a1" });
        _mockDisplayNameService.Setup(x => x.GetDisplayName(account)).Returns("Name");
        _mockDisplayNameService.Setup(x => x.GetDisplayName(recruiterAccount)).Returns("Recruiter");
        _mockVariablesService.Setup(x => x.GetVariable("RECRUITMENT_ENTRY_AGE")).Returns(new DomainVariableItem { Key = "RECRUITMENT_ENTRY_AGE", Item = "16" });

        var result = _subject.GetApplication(account);

        result.NextCandidateOp.Should().NotBeNullOrEmpty();
        result.NextCandidateOp.Should().BeOneOf("Today", "Tomorrow", "Tuesday", "Thursday", "Friday");
    }

    [Fact]
    public void GetApplication_AverageProcessingTime_IncludedInResult()
    {
        var now = DateTime.UtcNow;
        var recruiterAccount = CreateAccount("r1");
        var account = new DomainAccount
        {
            Id = "a1",
            Dob = now.AddYears(-22),
            Steamname = "steam",
            Application = new DomainApplication
            {
                State = ApplicationState.Waiting,
                Recruiter = "r1",
                DateCreated = now.AddDays(-5),
                DateAccepted = DateTime.MinValue
            }
        };

        var processedAccount = new DomainAccount
        {
            Id = "a2",
            Application = new DomainApplication
            {
                State = ApplicationState.Accepted,
                Recruiter = "r1",
                DateCreated = now.AddDays(-14),
                DateAccepted = now.AddDays(-7)
            }
        };

        _mockAccountContext.Setup(x => x.GetSingle("r1")).Returns(recruiterAccount);
        _mockAccountContext.Setup(x => x.Get(It.IsAny<Func<DomainAccount, bool>>()))
                           .Returns((Func<DomainAccount, bool> predicate) =>
                               {
                                   var allAccounts = new List<DomainAccount> { account, processedAccount };
                                   return allAccounts.Where(predicate);
                               }
                           );
        _mockAccountMapper.Setup(x => x.MapToAccount(account)).Returns(new Account { Id = "a1" });
        _mockDisplayNameService.Setup(x => x.GetDisplayName(account)).Returns("Name");
        _mockDisplayNameService.Setup(x => x.GetDisplayName(recruiterAccount)).Returns("Recruiter");
        _mockVariablesService.Setup(x => x.GetVariable("RECRUITMENT_ENTRY_AGE")).Returns(new DomainVariableItem { Key = "RECRUITMENT_ENTRY_AGE", Item = "16" });

        var result = _subject.GetApplication(account);

        result.AverageProcessingTime.Should().Be(7);
    }

    #endregion
}
