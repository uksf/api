using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using MongoDB.Bson;
using Moq;
using UKSF.Api.Interfaces.Admin;
using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Interfaces.Integrations.Teamspeak;
using UKSF.Api.Interfaces.Personnel;
using UKSF.Api.Interfaces.Units;
using UKSF.Api.Models.Admin;
using UKSF.Api.Models.Integrations;
using UKSF.Api.Models.Personnel;
using UKSF.Api.Models.Units;
using UKSF.Api.Services.Integrations.Teamspeak;
using UKSF.Api.Services.Units;
using Xunit;
using UksfUnit = UKSF.Api.Models.Units.Unit;

namespace UKSF.Tests.Unit.Services.Integrations.Teamspeak {
    public class TeamspeakGroupServiceTests {
        private static readonly VariableItem TEAMSPEAK_GID_UNVERIFIED = new VariableItem { key = "TEAMSPEAK_GID_UNVERIFIED", item = "1" };
        private static readonly VariableItem TEAMSPEAK_GID_DISCHARGED = new VariableItem { key = "TEAMSPEAK_GID_DISCHARGED", item = "2" };
        private static readonly VariableItem TEAMSPEAK_GID_ROOT = new VariableItem { key = "TEAMSPEAK_GID_ROOT", item = "3" };
        private static readonly VariableItem TEAMSPEAK_GID_ELCOM = new VariableItem { key = "TEAMSPEAK_GID_ELCOM", item = "4" };
        private static readonly VariableItem TEAMSPEAK_GID_BLACKLIST = new VariableItem { key = "TEAMSPEAK_GID_BLACKLIST", item = "99,100" };

        private readonly List<double> addedGroups = new List<double>();
        private readonly UksfUnit elcomUnit = new UksfUnit { id = ObjectId.GenerateNewId().ToString(), name = "ELCOM", branch = UnitBranch.AUXILIARY, parent = ObjectId.Empty.ToString() };
        private readonly Mock<IRanksDataService> mockRanksDataService = new Mock<IRanksDataService>();
        private readonly Mock<IRanksService> mockRanksService = new Mock<IRanksService>();
        private readonly Mock<IRolesService> mockRolesService = new Mock<IRolesService>();
        private readonly Mock<ITeamspeakManagerService> mockTeampeakManagerService = new Mock<ITeamspeakManagerService>();
        private readonly Mock<IUnitsDataService> mockUnitsDataService = new Mock<IUnitsDataService>();
        private readonly Mock<IVariablesService> mockVariablesService = new Mock<IVariablesService>();
        private readonly List<double> removedGroups = new List<double>();
        private readonly TeamspeakGroupService teamspeakGroupService;

        public TeamspeakGroupServiceTests() {
            mockRanksService.Setup(x => x.Data).Returns(mockRanksDataService.Object);

            mockVariablesService.Setup(x => x.GetVariable("TEAMSPEAK_GID_UNVERIFIED")).Returns(TEAMSPEAK_GID_UNVERIFIED);
            mockVariablesService.Setup(x => x.GetVariable("TEAMSPEAK_GID_DISCHARGED")).Returns(TEAMSPEAK_GID_DISCHARGED);
            mockVariablesService.Setup(x => x.GetVariable("TEAMSPEAK_GID_ROOT")).Returns(TEAMSPEAK_GID_ROOT);
            mockVariablesService.Setup(x => x.GetVariable("TEAMSPEAK_GID_ELCOM")).Returns(TEAMSPEAK_GID_ELCOM);
            mockVariablesService.Setup(x => x.GetVariable("TEAMSPEAK_GID_BLACKLIST")).Returns(TEAMSPEAK_GID_BLACKLIST);

            mockTeampeakManagerService.Setup(x => x.SendGroupProcedure(TeamspeakProcedureType.ASSIGN, It.IsAny<TeamspeakGroupProcedure>()))
                                      .Returns(Task.CompletedTask)
                                      .Callback((TeamspeakProcedureType _, TeamspeakGroupProcedure groupProcedure) => addedGroups.Add(groupProcedure.serverGroup));
            mockTeampeakManagerService.Setup(x => x.SendGroupProcedure(TeamspeakProcedureType.UNASSIGN, It.IsAny<TeamspeakGroupProcedure>()))
                                      .Returns(Task.CompletedTask)
                                      .Callback((TeamspeakProcedureType _, TeamspeakGroupProcedure groupProcedure) => removedGroups.Add(groupProcedure.serverGroup));

            IUnitsService unitsService = new UnitsService(mockUnitsDataService.Object, mockRolesService.Object);
            teamspeakGroupService = new TeamspeakGroupService(mockRanksService.Object, unitsService, mockTeampeakManagerService.Object, mockVariablesService.Object);
        }

        [Fact]
        public async Task Should_add_correct_groups_for_discharged() {
            await teamspeakGroupService.UpdateAccountGroups(new Account { membershipState = MembershipState.DISCHARGED }, new List<double>(), 2);

            addedGroups.Should().BeEquivalentTo(2);
            removedGroups.Should().BeEmpty();
        }

        [Fact]
        public async Task Should_add_correct_groups_for_elcom() {
            string id = ObjectId.GenerateNewId().ToString();
            string parentId = ObjectId.GenerateNewId().ToString();
            string parentParentId = ObjectId.GenerateNewId().ToString();
            UksfUnit unit = new UksfUnit { name = "1 Section", teamspeakGroup = "6", members = new List<string> { id }, parent = parentId };
            UksfUnit unitParent = new UksfUnit { id = parentId, name = "SFSG", teamspeakGroup = "7", parent = parentParentId };
            UksfUnit unitParentParent = new UksfUnit { id = parentParentId, name = "UKSF", teamspeakGroup = "8" };
            UksfUnit auxiliaryUnit = new UksfUnit { branch = UnitBranch.AUXILIARY, name = "SR1", teamspeakGroup = "9", parent = elcomUnit.id, members = new List<string> { id } };
            List<UksfUnit> units = new List<UksfUnit> { unit, unitParent, unitParentParent, elcomUnit, auxiliaryUnit };
            elcomUnit.members.Add(id);

            mockUnitsDataService.Setup(x => x.Get()).Returns(units);
            mockUnitsDataService.Setup(x => x.Get(It.IsAny<Func<UksfUnit, bool>>())).Returns<Func<UksfUnit, bool>>(predicate => units.Where(predicate));
            mockUnitsDataService.Setup(x => x.GetSingle(It.IsAny<Func<UksfUnit, bool>>())).Returns<Func<UksfUnit, bool>>(predicate => units.FirstOrDefault(predicate));
            mockRanksDataService.Setup(x => x.GetSingle("Private")).Returns(new Rank { name = "Private", teamspeakGroup = "5" });

            await teamspeakGroupService.UpdateAccountGroups(new Account { id = id, membershipState = MembershipState.MEMBER, rank = "Private", unitAssignment = "1 Section" }, new List<double>(), 2);

            addedGroups.Should().BeEquivalentTo(3, 4, 5, 7, 9);
            removedGroups.Should().BeEmpty();
        }

        [Fact]
        public async Task Should_add_correct_groups_for_member() {
            string id = ObjectId.GenerateNewId().ToString();
            string parentId = ObjectId.GenerateNewId().ToString();
            string parentParentId = ObjectId.GenerateNewId().ToString();
            UksfUnit unit = new UksfUnit { name = "1 Section", teamspeakGroup = "6", members = new List<string> { id }, parent = parentId };
            UksfUnit unitParent = new UksfUnit { id = parentId, name = "SFSG", teamspeakGroup = "7", parent = parentParentId };
            UksfUnit unitParentParent = new UksfUnit { id = parentParentId, name = "UKSF", teamspeakGroup = "8" };
            UksfUnit auxiliaryUnit = new UksfUnit { branch = UnitBranch.AUXILIARY, name = "SR1", teamspeakGroup = "9", parent = elcomUnit.id, members = new List<string> { id } };
            List<UksfUnit> units = new List<UksfUnit> { unit, unitParent, unitParentParent, elcomUnit, auxiliaryUnit };

            mockUnitsDataService.Setup(x => x.Get()).Returns(units);
            mockUnitsDataService.Setup(x => x.Get(It.IsAny<Func<UksfUnit, bool>>())).Returns<Func<UksfUnit, bool>>(predicate => units.Where(predicate));
            mockUnitsDataService.Setup(x => x.GetSingle(It.IsAny<Func<UksfUnit, bool>>())).Returns<Func<UksfUnit, bool>>(predicate => units.FirstOrDefault(predicate));
            mockRanksDataService.Setup(x => x.GetSingle("Private")).Returns(new Rank { name = "Private", teamspeakGroup = "5" });

            await teamspeakGroupService.UpdateAccountGroups(new Account { id = id, membershipState = MembershipState.MEMBER, rank = "Private", unitAssignment = "1 Section" }, new List<double>(), 2);

            addedGroups.Should().BeEquivalentTo(3, 5, 6, 7, 9);
            removedGroups.Should().BeEmpty();
        }

        [Fact]
        public async Task Should_add_correct_groups_for_candidate() {
            string id = ObjectId.GenerateNewId().ToString();

            mockRanksDataService.Setup(x => x.GetSingle("Candidate")).Returns(new Rank { name = "Candidate", teamspeakGroup = "5" });

            await teamspeakGroupService.UpdateAccountGroups(new Account { id = id, membershipState = MembershipState.CONFIRMED, rank = "Candidate" }, new List<double>(), 2);

            addedGroups.Should().BeEquivalentTo(5);
            removedGroups.Should().BeEmpty();
        }

        [Fact]
        public async Task Should_add_correct_groups_for_member_with_gaps_in_parents() {
            string id = ObjectId.GenerateNewId().ToString();
            string parentId = ObjectId.GenerateNewId().ToString();
            string parentParentId = ObjectId.GenerateNewId().ToString();
            string parentParentParentId = ObjectId.GenerateNewId().ToString();
            UksfUnit unit = new UksfUnit { name = "1 Section", members = new List<string> { id }, parent = parentId };
            UksfUnit unitParent = new UksfUnit { id = parentId, name = "1 Platoon", teamspeakGroup = "7", parent = parentParentId };
            UksfUnit unitParentParent = new UksfUnit { id = parentParentId, name = "A Company", parent = parentParentParentId };
            UksfUnit unitParentParentParent = new UksfUnit { id = parentParentParentId, name = "SFSG", teamspeakGroup = "8" };
            List<UksfUnit> units = new List<UksfUnit> { unit, unitParent, unitParentParent, unitParentParentParent, elcomUnit };

            mockUnitsDataService.Setup(x => x.Get()).Returns(units);
            mockUnitsDataService.Setup(x => x.Get(It.IsAny<Func<UksfUnit, bool>>())).Returns<Func<UksfUnit, bool>>(predicate => units.Where(predicate));
            mockUnitsDataService.Setup(x => x.GetSingle(It.IsAny<Func<UksfUnit, bool>>())).Returns<Func<UksfUnit, bool>>(predicate => units.FirstOrDefault(predicate));
            mockRanksDataService.Setup(x => x.GetSingle("Private")).Returns(new Rank { name = "Private", teamspeakGroup = "5" });

            await teamspeakGroupService.UpdateAccountGroups(new Account { id = id, membershipState = MembershipState.MEMBER, rank = "Private", unitAssignment = "1 Section" }, new List<double>(), 2);

            addedGroups.Should().BeEquivalentTo(3, 5, 7, 8);
            removedGroups.Should().BeEmpty();
        }

        [Fact]
        public async Task Should_add_correct_groups_for_non_member_with_no_account() {
            await teamspeakGroupService.UpdateAccountGroups(null, new List<double>(), 2);

            addedGroups.Should().BeEquivalentTo(1);
            removedGroups.Should().BeEmpty();
        }

        [Fact]
        public async Task Should_add_correct_groups_for_non_member() {
            await teamspeakGroupService.UpdateAccountGroups(new Account { membershipState = MembershipState.UNCONFIRMED }, new List<double>(), 2);

            addedGroups.Should().BeEquivalentTo(1);
            removedGroups.Should().BeEmpty();
        }

        [Fact]
        public async Task Should_add_correct_groups_for_stratcom() {
            string id = ObjectId.GenerateNewId().ToString();
            UksfUnit rootUnit = new UksfUnit { name = "UKSF", teamspeakGroup = "10", members = new List<string> { id }, parent = ObjectId.Empty.ToString() };
            UksfUnit auxiliaryUnit = new UksfUnit { branch = UnitBranch.AUXILIARY, name = "SR1", teamspeakGroup = "9", parent = elcomUnit.id, members = new List<string> { id } };
            List<UksfUnit> units = new List<UksfUnit> { rootUnit, elcomUnit, auxiliaryUnit };
            elcomUnit.members.Add(id);

            mockUnitsDataService.Setup(x => x.Get()).Returns(units);
            mockUnitsDataService.Setup(x => x.Get(It.IsAny<Func<UksfUnit, bool>>())).Returns<Func<UksfUnit, bool>>(predicate => units.Where(predicate));
            mockUnitsDataService.Setup(x => x.GetSingle(It.IsAny<Func<UksfUnit, bool>>())).Returns<Func<UksfUnit, bool>>(predicate => units.FirstOrDefault(predicate));
            mockRanksDataService.Setup(x => x.GetSingle("Private")).Returns(new Rank { name = "Private", teamspeakGroup = "5" });

            await teamspeakGroupService.UpdateAccountGroups(new Account { id = id, membershipState = MembershipState.MEMBER, rank = "Private", unitAssignment = "UKSF" }, new List<double>(), 2);

            addedGroups.Should().BeEquivalentTo(3, 4, 5, 10, 9);
            removedGroups.Should().BeEmpty();
        }

        [Fact]
        public async Task Should_add_correct_groups_for_first_root_child() {
            string id = ObjectId.GenerateNewId().ToString();
            string rootId = ObjectId.GenerateNewId().ToString();
            UksfUnit rootUnit = new UksfUnit { id = rootId, name = "UKSF", teamspeakGroup = "10", parent = ObjectId.Empty.ToString() };
            UksfUnit unit = new UksfUnit { name = "JSFAW", teamspeakGroup = "6", members = new List<string> { id }, parent = rootId };
            UksfUnit auxiliaryUnit = new UksfUnit { branch = UnitBranch.AUXILIARY, name = "SR1", teamspeakGroup = "9", parent = elcomUnit.id, members = new List<string> { id } };
            List<UksfUnit> units = new List<UksfUnit> { rootUnit, unit, elcomUnit, auxiliaryUnit };

            mockUnitsDataService.Setup(x => x.Get()).Returns(units);
            mockUnitsDataService.Setup(x => x.Get(It.IsAny<Func<UksfUnit, bool>>())).Returns<Func<UksfUnit, bool>>(predicate => units.Where(predicate));
            mockUnitsDataService.Setup(x => x.GetSingle(It.IsAny<Func<UksfUnit, bool>>())).Returns<Func<UksfUnit, bool>>(predicate => units.FirstOrDefault(predicate));
            mockRanksDataService.Setup(x => x.GetSingle("Private")).Returns(new Rank { name = "Private", teamspeakGroup = "5" });

            await teamspeakGroupService.UpdateAccountGroups(new Account { id = id, membershipState = MembershipState.MEMBER, rank = "Private", unitAssignment = "JSFAW" }, new List<double>(), 2);

            addedGroups.Should().BeEquivalentTo(3, 5, 6, 9);
            removedGroups.Should().BeEmpty();
        }

        [Fact]
        public async Task Should_add_correct_groups_for_first_root_child_in_elcom() {
            string id = ObjectId.GenerateNewId().ToString();
            string rootId = ObjectId.GenerateNewId().ToString();
            UksfUnit rootUnit = new UksfUnit { id = rootId, name = "UKSF", teamspeakGroup = "10", parent = ObjectId.Empty.ToString() };
            UksfUnit unit = new UksfUnit { name = "JSFAW", teamspeakGroup = "6", members = new List<string> { id }, parent = rootId };
            UksfUnit auxiliaryUnit = new UksfUnit { branch = UnitBranch.AUXILIARY, name = "SR1", teamspeakGroup = "9", parent = elcomUnit.id, members = new List<string> { id } };
            List<UksfUnit> units = new List<UksfUnit> { rootUnit, unit, elcomUnit, auxiliaryUnit };
            elcomUnit.members.Add(id);

            mockUnitsDataService.Setup(x => x.Get()).Returns(units);
            mockUnitsDataService.Setup(x => x.Get(It.IsAny<Func<UksfUnit, bool>>())).Returns<Func<UksfUnit, bool>>(predicate => units.Where(predicate));
            mockUnitsDataService.Setup(x => x.GetSingle(It.IsAny<Func<UksfUnit, bool>>())).Returns<Func<UksfUnit, bool>>(predicate => units.FirstOrDefault(predicate));
            mockRanksDataService.Setup(x => x.GetSingle("Private")).Returns(new Rank { name = "Private", teamspeakGroup = "5" });

            await teamspeakGroupService.UpdateAccountGroups(new Account { id = id, membershipState = MembershipState.MEMBER, rank = "Private", unitAssignment = "JSFAW" }, new List<double>(), 2);

            addedGroups.Should().BeEquivalentTo(3, 5, 4, 6, 9);
            removedGroups.Should().BeEmpty();
        }

        [Fact]
        public async Task Should_only_add_groups_if_not_set() {
            string id = ObjectId.GenerateNewId().ToString();
            string parentId = ObjectId.GenerateNewId().ToString();
            string parentParentId = ObjectId.GenerateNewId().ToString();
            UksfUnit unit = new UksfUnit { name = "1 Section", teamspeakGroup = "6", members = new List<string> { id }, parent = parentId };
            UksfUnit unitParent = new UksfUnit { id = parentId, name = "SFSG", teamspeakGroup = "7", parent = parentParentId };
            UksfUnit unitParentParent = new UksfUnit { id = parentParentId, name = "UKSF", teamspeakGroup = "8" };
            UksfUnit auxiliaryUnit = new UksfUnit { branch = UnitBranch.AUXILIARY, name = "SR1", teamspeakGroup = "9", parent = elcomUnit.id, members = new List<string> { id } };
            List<UksfUnit> units = new List<UksfUnit> { unit, unitParent, unitParentParent, elcomUnit, auxiliaryUnit };

            mockUnitsDataService.Setup(x => x.Get()).Returns(units);
            mockUnitsDataService.Setup(x => x.Get(It.IsAny<Func<UksfUnit, bool>>())).Returns<Func<UksfUnit, bool>>(predicate => units.Where(predicate));
            mockUnitsDataService.Setup(x => x.GetSingle(It.IsAny<Func<UksfUnit, bool>>())).Returns<Func<UksfUnit, bool>>(predicate => units.FirstOrDefault(predicate));
            mockRanksDataService.Setup(x => x.GetSingle("Private")).Returns(new Rank { name = "Private", teamspeakGroup = "5" });

            await teamspeakGroupService.UpdateAccountGroups(
                new Account { id = id, membershipState = MembershipState.MEMBER, rank = "Private", unitAssignment = "1 Section" },
                new List<double> { 3, 5 },
                2
            );

            addedGroups.Should().BeEquivalentTo(6, 7, 9);
            removedGroups.Should().BeEmpty();
        }

        [Fact]
        public async Task Should_remove_correct_groups() {
            string id = ObjectId.GenerateNewId().ToString();
            string parentId = ObjectId.GenerateNewId().ToString();
            string parentParentId = ObjectId.GenerateNewId().ToString();
            UksfUnit unit = new UksfUnit { name = "1 Section", teamspeakGroup = "6", members = new List<string> { id }, parent = parentId };
            UksfUnit unitParent = new UksfUnit { id = parentId, name = "SFSG", teamspeakGroup = "7", parent = parentParentId };
            UksfUnit unitParentParent = new UksfUnit { id = parentParentId, name = "UKSF", teamspeakGroup = "8" };
            UksfUnit auxiliaryUnit = new UksfUnit { branch = UnitBranch.AUXILIARY, name = "SR1", teamspeakGroup = "9", parent = elcomUnit.id, members = new List<string> { id } };
            List<UksfUnit> units = new List<UksfUnit> { unit, unitParent, unitParentParent, elcomUnit, auxiliaryUnit };

            mockUnitsDataService.Setup(x => x.Get()).Returns(units);
            mockUnitsDataService.Setup(x => x.Get(It.IsAny<Func<UksfUnit, bool>>())).Returns<Func<UksfUnit, bool>>(predicate => units.Where(predicate));
            mockUnitsDataService.Setup(x => x.GetSingle(It.IsAny<Func<UksfUnit, bool>>())).Returns<Func<UksfUnit, bool>>(predicate => units.FirstOrDefault(predicate));
            mockRanksDataService.Setup(x => x.GetSingle("Private")).Returns(new Rank { name = "Private", teamspeakGroup = "5" });

            await teamspeakGroupService.UpdateAccountGroups(
                new Account { id = id, membershipState = MembershipState.MEMBER, rank = "Private", unitAssignment = "1 Section" },
                new List<double> { 1, 10 },
                2
            );

            addedGroups.Should().BeEquivalentTo(3, 5, 6, 7, 9);
            removedGroups.Should().BeEquivalentTo(1, 10);
        }

        [Fact]
        public async Task Should_remove_groups() {
            await teamspeakGroupService.UpdateAccountGroups(null, new List<double> { 1, 3, 4 }, 2);

            addedGroups.Should().BeEmpty();
            removedGroups.Should().BeEquivalentTo(3, 4);
        }

        [Fact]
        public async Task Should_remove_groups_except_blacklisted() {
            await teamspeakGroupService.UpdateAccountGroups(null, new List<double> { 1, 3, 4, 99, 100 }, 2);

            addedGroups.Should().BeEmpty();
            removedGroups.Should().BeEquivalentTo(3, 4);
        }
    }
}
