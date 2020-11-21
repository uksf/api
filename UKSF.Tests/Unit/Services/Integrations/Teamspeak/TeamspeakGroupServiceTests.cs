using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using MongoDB.Bson;
using Moq;
using UKSF.Api.Admin.Models;
using UKSF.Api.Admin.Services;
using UKSF.Api.Personnel.Context;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Personnel.Services;
using UKSF.Api.Teamspeak.Models;
using UKSF.Api.Teamspeak.Services;
using Xunit;
using UksfUnit = UKSF.Api.Personnel.Models.Unit;

namespace UKSF.Tests.Unit.Services.Integrations.Teamspeak {
    public class TeamspeakGroupServiceTests {
        private static readonly VariableItem TEAMSPEAK_GID_UNVERIFIED = new() { Key = "TEAMSPEAK_GID_UNVERIFIED", Item = "1" };
        private static readonly VariableItem TEAMSPEAK_GID_DISCHARGED = new() { Key = "TEAMSPEAK_GID_DISCHARGED", Item = "2" };
        private static readonly VariableItem TEAMSPEAK_GID_ROOT = new() { Key = "TEAMSPEAK_GID_ROOT", Item = "3" };
        private static readonly VariableItem TEAMSPEAK_GID_ELCOM = new() { Key = "TEAMSPEAK_GID_ELCOM", Item = "4" };
        private static readonly VariableItem TEAMSPEAK_GID_BLACKLIST = new() { Key = "TEAMSPEAK_GID_BLACKLIST", Item = "99,100" };

        private readonly List<double> _addedGroups = new();
        private readonly UksfUnit _elcomUnit = new() { Id = ObjectId.GenerateNewId().ToString(), Name = "ELCOM", Branch = UnitBranch.AUXILIARY, Parent = ObjectId.Empty.ToString() };
        private readonly Mock<IRanksContext> _mockRanksContext = new();
        private readonly Mock<IRolesContext> _mockRolesContext = new();
        private readonly Mock<ITeamspeakManagerService> _mockTeampeakManagerService = new();
        private readonly Mock<IUnitsContext> _mockUnitsContext = new();
        private readonly Mock<IVariablesService> _mockVariablesService = new();
        private readonly List<double> _removedGroups = new();
        private readonly TeamspeakGroupService _teamspeakGroupService;

        public TeamspeakGroupServiceTests() {
            _mockVariablesService.Setup(x => x.GetVariable("TEAMSPEAK_GID_UNVERIFIED")).Returns(TEAMSPEAK_GID_UNVERIFIED);
            _mockVariablesService.Setup(x => x.GetVariable("TEAMSPEAK_GID_DISCHARGED")).Returns(TEAMSPEAK_GID_DISCHARGED);
            _mockVariablesService.Setup(x => x.GetVariable("TEAMSPEAK_GID_ROOT")).Returns(TEAMSPEAK_GID_ROOT);
            _mockVariablesService.Setup(x => x.GetVariable("TEAMSPEAK_GID_ELCOM")).Returns(TEAMSPEAK_GID_ELCOM);
            _mockVariablesService.Setup(x => x.GetVariable("TEAMSPEAK_GID_BLACKLIST")).Returns(TEAMSPEAK_GID_BLACKLIST);

            _mockTeampeakManagerService.Setup(x => x.SendGroupProcedure(TeamspeakProcedureType.ASSIGN, It.IsAny<TeamspeakGroupProcedure>()))
                                       .Returns(Task.CompletedTask)
                                       .Callback((TeamspeakProcedureType _, TeamspeakGroupProcedure groupProcedure) => _addedGroups.Add(groupProcedure.ServerGroup));
            _mockTeampeakManagerService.Setup(x => x.SendGroupProcedure(TeamspeakProcedureType.UNASSIGN, It.IsAny<TeamspeakGroupProcedure>()))
                                       .Returns(Task.CompletedTask)
                                       .Callback((TeamspeakProcedureType _, TeamspeakGroupProcedure groupProcedure) => _removedGroups.Add(groupProcedure.ServerGroup));

            IUnitsService unitsService = new UnitsService(_mockUnitsContext.Object, _mockRolesContext.Object);
            _teamspeakGroupService = new TeamspeakGroupService(_mockRanksContext.Object, _mockUnitsContext.Object, unitsService, _mockTeampeakManagerService.Object, _mockVariablesService.Object);
        }

        [Fact]
        public async Task Should_add_correct_groups_for_candidate() {
            string id = ObjectId.GenerateNewId().ToString();

            _mockRanksContext.Setup(x => x.GetSingle("Candidate")).Returns(new Rank { Name = "Candidate", TeamspeakGroup = "5" });

            await _teamspeakGroupService.UpdateAccountGroups(new Account { Id = id, MembershipState = MembershipState.CONFIRMED, Rank = "Candidate" }, new List<double>(), 2);

            _addedGroups.Should().BeEquivalentTo(5);
            _removedGroups.Should().BeEmpty();
        }

        [Fact]
        public async Task Should_add_correct_groups_for_discharged() {
            await _teamspeakGroupService.UpdateAccountGroups(new Account { MembershipState = MembershipState.DISCHARGED }, new List<double>(), 2);

            _addedGroups.Should().BeEquivalentTo(2);
            _removedGroups.Should().BeEmpty();
        }

        [Fact]
        public async Task Should_add_correct_groups_for_elcom() {
            string id = ObjectId.GenerateNewId().ToString();
            string parentId = ObjectId.GenerateNewId().ToString();
            string parentParentId = ObjectId.GenerateNewId().ToString();
            UksfUnit unit = new() { Name = "1 Section", TeamspeakGroup = "6", Members = new List<string> { id }, Parent = parentId };
            UksfUnit unitParent = new() { Id = parentId, Name = "SFSG", TeamspeakGroup = "7", Parent = parentParentId };
            UksfUnit unitParentParent = new() { Id = parentParentId, Name = "UKSF", TeamspeakGroup = "8" };
            UksfUnit auxiliaryUnit = new() { Branch = UnitBranch.AUXILIARY, Name = "SR1", TeamspeakGroup = "9", Parent = _elcomUnit.Id, Members = new List<string> { id } };
            List<UksfUnit> units = new() { unit, unitParent, unitParentParent, _elcomUnit, auxiliaryUnit };
            _elcomUnit.Members.Add(id);

            _mockUnitsContext.Setup(x => x.Get()).Returns(units);
            _mockUnitsContext.Setup(x => x.Get(It.IsAny<Func<UksfUnit, bool>>())).Returns<Func<UksfUnit, bool>>(predicate => units.Where(predicate));
            _mockUnitsContext.Setup(x => x.GetSingle(It.IsAny<Func<UksfUnit, bool>>())).Returns<Func<UksfUnit, bool>>(predicate => units.FirstOrDefault(predicate));
            _mockRanksContext.Setup(x => x.GetSingle("Private")).Returns(new Rank { Name = "Private", TeamspeakGroup = "5" });

            await _teamspeakGroupService.UpdateAccountGroups(new Account { Id = id, MembershipState = MembershipState.MEMBER, Rank = "Private", UnitAssignment = "1 Section" }, new List<double>(), 2);

            _addedGroups.Should().BeEquivalentTo(3, 4, 5, 7, 9);
            _removedGroups.Should().BeEmpty();
        }

        [Fact]
        public async Task Should_add_correct_groups_for_first_root_child() {
            string id = ObjectId.GenerateNewId().ToString();
            string rootId = ObjectId.GenerateNewId().ToString();
            UksfUnit rootUnit = new() { Id = rootId, Name = "UKSF", TeamspeakGroup = "10", Parent = ObjectId.Empty.ToString() };
            UksfUnit unit = new() { Name = "JSFAW", TeamspeakGroup = "6", Members = new List<string> { id }, Parent = rootId };
            UksfUnit auxiliaryUnit = new() { Branch = UnitBranch.AUXILIARY, Name = "SR1", TeamspeakGroup = "9", Parent = _elcomUnit.Id, Members = new List<string> { id } };
            List<UksfUnit> units = new() { rootUnit, unit, _elcomUnit, auxiliaryUnit };

            _mockUnitsContext.Setup(x => x.Get()).Returns(units);
            _mockUnitsContext.Setup(x => x.Get(It.IsAny<Func<UksfUnit, bool>>())).Returns<Func<UksfUnit, bool>>(predicate => units.Where(predicate));
            _mockUnitsContext.Setup(x => x.GetSingle(It.IsAny<Func<UksfUnit, bool>>())).Returns<Func<UksfUnit, bool>>(predicate => units.FirstOrDefault(predicate));
            _mockRanksContext.Setup(x => x.GetSingle("Private")).Returns(new Rank { Name = "Private", TeamspeakGroup = "5" });

            await _teamspeakGroupService.UpdateAccountGroups(new Account { Id = id, MembershipState = MembershipState.MEMBER, Rank = "Private", UnitAssignment = "JSFAW" }, new List<double>(), 2);

            _addedGroups.Should().BeEquivalentTo(3, 5, 6, 9);
            _removedGroups.Should().BeEmpty();
        }

        [Fact]
        public async Task Should_add_correct_groups_for_first_root_child_in_elcom() {
            string id = ObjectId.GenerateNewId().ToString();
            string rootId = ObjectId.GenerateNewId().ToString();
            UksfUnit rootUnit = new() { Id = rootId, Name = "UKSF", TeamspeakGroup = "10", Parent = ObjectId.Empty.ToString() };
            UksfUnit unit = new() { Name = "JSFAW", TeamspeakGroup = "6", Members = new List<string> { id }, Parent = rootId };
            UksfUnit auxiliaryUnit = new() { Branch = UnitBranch.AUXILIARY, Name = "SR1", TeamspeakGroup = "9", Parent = _elcomUnit.Id, Members = new List<string> { id } };
            List<UksfUnit> units = new() { rootUnit, unit, _elcomUnit, auxiliaryUnit };
            _elcomUnit.Members.Add(id);

            _mockUnitsContext.Setup(x => x.Get()).Returns(units);
            _mockUnitsContext.Setup(x => x.Get(It.IsAny<Func<UksfUnit, bool>>())).Returns<Func<UksfUnit, bool>>(predicate => units.Where(predicate));
            _mockUnitsContext.Setup(x => x.GetSingle(It.IsAny<Func<UksfUnit, bool>>())).Returns<Func<UksfUnit, bool>>(predicate => units.FirstOrDefault(predicate));
            _mockRanksContext.Setup(x => x.GetSingle("Private")).Returns(new Rank { Name = "Private", TeamspeakGroup = "5" });

            await _teamspeakGroupService.UpdateAccountGroups(new Account { Id = id, MembershipState = MembershipState.MEMBER, Rank = "Private", UnitAssignment = "JSFAW" }, new List<double>(), 2);

            _addedGroups.Should().BeEquivalentTo(3, 5, 4, 6, 9);
            _removedGroups.Should().BeEmpty();
        }

        [Fact]
        public async Task Should_add_correct_groups_for_member() {
            string id = ObjectId.GenerateNewId().ToString();
            string parentId = ObjectId.GenerateNewId().ToString();
            string parentParentId = ObjectId.GenerateNewId().ToString();
            UksfUnit unit = new() { Name = "1 Section", TeamspeakGroup = "6", Members = new List<string> { id }, Parent = parentId };
            UksfUnit unitParent = new() { Id = parentId, Name = "SFSG", TeamspeakGroup = "7", Parent = parentParentId };
            UksfUnit unitParentParent = new() { Id = parentParentId, Name = "UKSF", TeamspeakGroup = "8" };
            UksfUnit auxiliaryUnit = new() { Branch = UnitBranch.AUXILIARY, Name = "SR1", TeamspeakGroup = "9", Parent = _elcomUnit.Id, Members = new List<string> { id } };
            List<UksfUnit> units = new() { unit, unitParent, unitParentParent, _elcomUnit, auxiliaryUnit };

            _mockUnitsContext.Setup(x => x.Get()).Returns(units);
            _mockUnitsContext.Setup(x => x.Get(It.IsAny<Func<UksfUnit, bool>>())).Returns<Func<UksfUnit, bool>>(predicate => units.Where(predicate));
            _mockUnitsContext.Setup(x => x.GetSingle(It.IsAny<Func<UksfUnit, bool>>())).Returns<Func<UksfUnit, bool>>(predicate => units.FirstOrDefault(predicate));
            _mockRanksContext.Setup(x => x.GetSingle("Private")).Returns(new Rank { Name = "Private", TeamspeakGroup = "5" });

            await _teamspeakGroupService.UpdateAccountGroups(new Account { Id = id, MembershipState = MembershipState.MEMBER, Rank = "Private", UnitAssignment = "1 Section" }, new List<double>(), 2);

            _addedGroups.Should().BeEquivalentTo(3, 5, 6, 7, 9);
            _removedGroups.Should().BeEmpty();
        }

        [Fact]
        public async Task Should_add_correct_groups_for_member_with_gaps_in_parents() {
            string id = ObjectId.GenerateNewId().ToString();
            string parentId = ObjectId.GenerateNewId().ToString();
            string parentParentId = ObjectId.GenerateNewId().ToString();
            string parentParentParentId = ObjectId.GenerateNewId().ToString();
            UksfUnit unit = new() { Name = "1 Section", Members = new List<string> { id }, Parent = parentId };
            UksfUnit unitParent = new() { Id = parentId, Name = "1 Platoon", TeamspeakGroup = "7", Parent = parentParentId };
            UksfUnit unitParentParent = new() { Id = parentParentId, Name = "A Company", Parent = parentParentParentId };
            UksfUnit unitParentParentParent = new() { Id = parentParentParentId, Name = "SFSG", TeamspeakGroup = "8" };
            List<UksfUnit> units = new() { unit, unitParent, unitParentParent, unitParentParentParent, _elcomUnit };

            _mockUnitsContext.Setup(x => x.Get()).Returns(units);
            _mockUnitsContext.Setup(x => x.Get(It.IsAny<Func<UksfUnit, bool>>())).Returns<Func<UksfUnit, bool>>(predicate => units.Where(predicate));
            _mockUnitsContext.Setup(x => x.GetSingle(It.IsAny<Func<UksfUnit, bool>>())).Returns<Func<UksfUnit, bool>>(predicate => units.FirstOrDefault(predicate));
            _mockRanksContext.Setup(x => x.GetSingle("Private")).Returns(new Rank { Name = "Private", TeamspeakGroup = "5" });

            await _teamspeakGroupService.UpdateAccountGroups(new Account { Id = id, MembershipState = MembershipState.MEMBER, Rank = "Private", UnitAssignment = "1 Section" }, new List<double>(), 2);

            _addedGroups.Should().BeEquivalentTo(3, 5, 7, 8);
            _removedGroups.Should().BeEmpty();
        }

        [Fact]
        public async Task Should_add_correct_groups_for_non_member() {
            await _teamspeakGroupService.UpdateAccountGroups(new Account { MembershipState = MembershipState.UNCONFIRMED }, new List<double>(), 2);

            _addedGroups.Should().BeEquivalentTo(1);
            _removedGroups.Should().BeEmpty();
        }

        [Fact]
        public async Task Should_add_correct_groups_for_non_member_with_no_account() {
            await _teamspeakGroupService.UpdateAccountGroups(null, new List<double>(), 2);

            _addedGroups.Should().BeEquivalentTo(1);
            _removedGroups.Should().BeEmpty();
        }

        [Fact]
        public async Task Should_add_correct_groups_for_stratcom() {
            string id = ObjectId.GenerateNewId().ToString();
            UksfUnit rootUnit = new() { Name = "UKSF", TeamspeakGroup = "10", Members = new List<string> { id }, Parent = ObjectId.Empty.ToString() };
            UksfUnit auxiliaryUnit = new() { Branch = UnitBranch.AUXILIARY, Name = "SR1", TeamspeakGroup = "9", Parent = _elcomUnit.Id, Members = new List<string> { id } };
            List<UksfUnit> units = new() { rootUnit, _elcomUnit, auxiliaryUnit };
            _elcomUnit.Members.Add(id);

            _mockUnitsContext.Setup(x => x.Get()).Returns(units);
            _mockUnitsContext.Setup(x => x.Get(It.IsAny<Func<UksfUnit, bool>>())).Returns<Func<UksfUnit, bool>>(predicate => units.Where(predicate));
            _mockUnitsContext.Setup(x => x.GetSingle(It.IsAny<Func<UksfUnit, bool>>())).Returns<Func<UksfUnit, bool>>(predicate => units.FirstOrDefault(predicate));
            _mockRanksContext.Setup(x => x.GetSingle("Private")).Returns(new Rank { Name = "Private", TeamspeakGroup = "5" });

            await _teamspeakGroupService.UpdateAccountGroups(new Account { Id = id, MembershipState = MembershipState.MEMBER, Rank = "Private", UnitAssignment = "UKSF" }, new List<double>(), 2);

            _addedGroups.Should().BeEquivalentTo(3, 4, 5, 10, 9);
            _removedGroups.Should().BeEmpty();
        }

        [Fact]
        public async Task Should_only_add_groups_if_not_set() {
            string id = ObjectId.GenerateNewId().ToString();
            string parentId = ObjectId.GenerateNewId().ToString();
            string parentParentId = ObjectId.GenerateNewId().ToString();
            UksfUnit unit = new() { Name = "1 Section", TeamspeakGroup = "6", Members = new List<string> { id }, Parent = parentId };
            UksfUnit unitParent = new() { Id = parentId, Name = "SFSG", TeamspeakGroup = "7", Parent = parentParentId };
            UksfUnit unitParentParent = new() { Id = parentParentId, Name = "UKSF", TeamspeakGroup = "8" };
            UksfUnit auxiliaryUnit = new() { Branch = UnitBranch.AUXILIARY, Name = "SR1", TeamspeakGroup = "9", Parent = _elcomUnit.Id, Members = new List<string> { id } };
            List<UksfUnit> units = new() { unit, unitParent, unitParentParent, _elcomUnit, auxiliaryUnit };

            _mockUnitsContext.Setup(x => x.Get()).Returns(units);
            _mockUnitsContext.Setup(x => x.Get(It.IsAny<Func<UksfUnit, bool>>())).Returns<Func<UksfUnit, bool>>(predicate => units.Where(predicate));
            _mockUnitsContext.Setup(x => x.GetSingle(It.IsAny<Func<UksfUnit, bool>>())).Returns<Func<UksfUnit, bool>>(predicate => units.FirstOrDefault(predicate));
            _mockRanksContext.Setup(x => x.GetSingle("Private")).Returns(new Rank { Name = "Private", TeamspeakGroup = "5" });

            await _teamspeakGroupService.UpdateAccountGroups(
                new Account { Id = id, MembershipState = MembershipState.MEMBER, Rank = "Private", UnitAssignment = "1 Section" },
                new List<double> { 3, 5 },
                2
            );

            _addedGroups.Should().BeEquivalentTo(6, 7, 9);
            _removedGroups.Should().BeEmpty();
        }

        [Fact]
        public async Task Should_remove_correct_groups() {
            string id = ObjectId.GenerateNewId().ToString();
            string parentId = ObjectId.GenerateNewId().ToString();
            string parentParentId = ObjectId.GenerateNewId().ToString();
            UksfUnit unit = new() { Name = "1 Section", TeamspeakGroup = "6", Members = new List<string> { id }, Parent = parentId };
            UksfUnit unitParent = new() { Id = parentId, Name = "SFSG", TeamspeakGroup = "7", Parent = parentParentId };
            UksfUnit unitParentParent = new() { Id = parentParentId, Name = "UKSF", TeamspeakGroup = "8" };
            UksfUnit auxiliaryUnit = new() { Branch = UnitBranch.AUXILIARY, Name = "SR1", TeamspeakGroup = "9", Parent = _elcomUnit.Id, Members = new List<string> { id } };
            List<UksfUnit> units = new() { unit, unitParent, unitParentParent, _elcomUnit, auxiliaryUnit };

            _mockUnitsContext.Setup(x => x.Get()).Returns(units);
            _mockUnitsContext.Setup(x => x.Get(It.IsAny<Func<UksfUnit, bool>>())).Returns<Func<UksfUnit, bool>>(predicate => units.Where(predicate));
            _mockUnitsContext.Setup(x => x.GetSingle(It.IsAny<Func<UksfUnit, bool>>())).Returns<Func<UksfUnit, bool>>(predicate => units.FirstOrDefault(predicate));
            _mockRanksContext.Setup(x => x.GetSingle("Private")).Returns(new Rank { Name = "Private", TeamspeakGroup = "5" });

            await _teamspeakGroupService.UpdateAccountGroups(
                new Account { Id = id, MembershipState = MembershipState.MEMBER, Rank = "Private", UnitAssignment = "1 Section" },
                new List<double> { 1, 10 },
                2
            );

            _addedGroups.Should().BeEquivalentTo(3, 5, 6, 7, 9);
            _removedGroups.Should().BeEquivalentTo(1, 10);
        }

        [Fact]
        public async Task Should_remove_groups() {
            await _teamspeakGroupService.UpdateAccountGroups(null, new List<double> { 1, 3, 4 }, 2);

            _addedGroups.Should().BeEmpty();
            _removedGroups.Should().BeEquivalentTo(3, 4);
        }

        [Fact]
        public async Task Should_remove_groups_except_blacklisted() {
            await _teamspeakGroupService.UpdateAccountGroups(null, new List<double> { 1, 3, 4, 99, 100 }, 2);

            _addedGroups.Should().BeEmpty();
            _removedGroups.Should().BeEquivalentTo(3, 4);
        }
    }
}
