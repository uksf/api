﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using MongoDB.Bson;
using MongoDB.Driver;
using UKSFWebsite.Api.Interfaces.Data.Cached;
using UKSFWebsite.Api.Interfaces.Personnel;
using UKSFWebsite.Api.Interfaces.Units;
using UKSFWebsite.Api.Models.Personnel;
using UKSFWebsite.Api.Models.Units;
using UKSFWebsite.Api.Services.Hubs;
using UKSFWebsite.Api.Services.Hubs.Abstraction;

namespace UKSFWebsite.Api.Services.Units {
    public class UnitsService : IUnitsService {
        private readonly IHubContext<AccountHub, IAccountClient> accountHub;
        private readonly IUnitsDataService data;
        private readonly IRolesService rolesService;

        public UnitsService(IUnitsDataService data, IRolesService rolesService, IHubContext<AccountHub, IAccountClient> accountHub) {
            this.data = data;
            this.rolesService = rolesService;
            this.accountHub = accountHub;
        }

        public IUnitsDataService Data() => data;

        public IEnumerable<Unit> GetSortedUnits(Func<Unit, bool> predicate = null) {
            List<Unit> sortedUnits = new List<Unit>();
            Unit combatRoot = data.GetSingle(x => x.parent == ObjectId.Empty.ToString() && x.branch == UnitBranch.COMBAT);
            Unit auxiliaryRoot = data.GetSingle(x => x.parent == ObjectId.Empty.ToString() && x.branch == UnitBranch.AUXILIARY);
            sortedUnits.Add(combatRoot);
            sortedUnits.AddRange(GetAllChildren(combatRoot));
            sortedUnits.Add(auxiliaryRoot);
            sortedUnits.AddRange(GetAllChildren(auxiliaryRoot));

            return predicate != null ? sortedUnits.Where(predicate).ToList() : sortedUnits;
        }

        public async Task AddMember(string id, string unitId) {
            if (data.GetSingle(x => x.id == unitId && x.members.Contains(id)) != null) return;
            await data.Update(unitId, Builders<Unit>.Update.Push(x => x.members, id));
            await accountHub.Clients.Group(id).ReceiveAccountUpdate();
        }

        public async Task RemoveMember(string id, string unitName) {
            Unit unit = data.GetSingle(x => x.name == unitName);
            if (unit == null) return;

            await RemoveMember(id, unit);
        }

        public async Task RemoveMember(string id, Unit unit) {
            if (unit.members.Contains(id)) {
                await data.Update(unit.id, Builders<Unit>.Update.Pull(x => x.members, id));
            }

            await RemoveMemberRoles(id, unit);
            await accountHub.Clients.Group(id).ReceiveAccountUpdate();
        }

        public async Task SetMemberRole(string id, string unitId, string role = "") {
            Unit unit = data.GetSingle(x => x.id == unitId);
            if (unit == null) return;

            await SetMemberRole(id, unit, role);
        }

        public async Task SetMemberRole(string id, Unit unit, string role = "") {
            await RemoveMemberRoles(id, unit);
            if (!string.IsNullOrEmpty(role)) {
                await data.Update(unit.id, Builders<Unit>.Update.Set($"roles.{role}", id));
            }

            await accountHub.Clients.Group(id).ReceiveAccountUpdate();
        }

        public async Task RenameRole(string oldName, string newName) {
            foreach (Unit unit in data.Get(x => x.roles.ContainsKey(oldName))) {
                string id = unit.roles[oldName];
                await data.Update(unit.id, Builders<Unit>.Update.Unset($"roles.{oldName}"));
                await data.Update(unit.id, Builders<Unit>.Update.Set($"roles.{newName}", id));
            }
        }

        public async Task DeleteRole(string role) {
            foreach (Unit unit in data.Get(x => x.roles.ContainsKey(role))) {
                string id = unit.roles[role];
                await data.Update(unit.id, Builders<Unit>.Update.Unset($"roles.{role}"));
                await accountHub.Clients.Group(id).ReceiveAccountUpdate();
            }
        }

        public bool HasRole(string unitId, string role) {
            Unit unit = data.GetSingle(x => x.id == unitId);
            return HasRole(unit, role);
        }

        public bool HasRole(Unit unit, string role) => unit.roles.ContainsKey(role);

        public bool RolesHasMember(string unitId, string id) {
            Unit unit = data.GetSingle(x => x.id == unitId);
            return RolesHasMember(unit, id);
        }

        public bool RolesHasMember(Unit unit, string id) => unit.roles.ContainsValue(id);

        public bool MemberHasRole(string id, string unitId, string role) {
            Unit unit = data.GetSingle(x => x.id == unitId);
            return MemberHasRole(id, unit, role);
        }

        public bool MemberHasRole(string id, Unit unit, string role) => unit.roles.GetValueOrDefault(role, string.Empty) == id;

        public bool MemberHasAnyRole(string id) => data.Get().Any(x => RolesHasMember(x, id));

        public int GetMemberRoleOrder(Account account, Unit unit) {
            if (RolesHasMember(unit.id, account.id)) {
                return int.MaxValue - rolesService.Data().GetSingle(x => x.name == unit.roles.FirstOrDefault(y => y.Value == account.id).Key).order;
            }

            return -1;
        }

        public Unit GetRoot() => data.GetSingle(x => x.parent == ObjectId.Empty.ToString() && x.branch == UnitBranch.COMBAT);

        public Unit GetAuxilliaryRoot() => data.GetSingle(x => x.parent == ObjectId.Empty.ToString() && x.branch == UnitBranch.AUXILIARY);

        public Unit GetParent(Unit unit) {
            return unit.parent != string.Empty ? data.GetSingle(x => x.id == unit.parent) : null;
        }

        public IEnumerable<Unit> GetParents(Unit unit) {
            if (unit == null) return new List<Unit>();
            List<Unit> parentUnits = new List<Unit>();
            do {
                parentUnits.Add(unit);
                Unit child = unit;
                unit = !string.IsNullOrEmpty(unit.parent) ? data.GetSingle(x => x.id == child.parent) : null;
            } while (unit != null);

            return parentUnits;
        }

        public IEnumerable<Unit> GetChildren(Unit parent) => data.Get(x => x.parent == parent.id).ToList();

        public IEnumerable<Unit> GetAllChildren(Unit parent, bool includeParent = false) {
            List<Unit> children = includeParent ? new List<Unit> {parent} : new List<Unit>();
            foreach (Unit unit in data.Get(x => x.parent == parent.id)) {
                children.Add(unit);
                children.AddRange(GetAllChildren(unit));
            }

            return children;
        }

        public int GetUnitDepth(Unit unit) {
            if (unit.parent == ObjectId.Empty.ToString()) {
                return 0;
            }

            int depth = 0;
            Unit parent = data.GetSingle(unit.parent);
            while (parent != null) {
                depth++;
                parent = data.GetSingle(parent.parent);
            }

            return depth;
        }

        public string GetChainString(Unit unit) {
            List<Unit> parentUnits = GetParents(unit).Skip(1).ToList();
            string unitNames = unit.name;
            parentUnits.ForEach(x => unitNames += $", {x.name}");
            return unitNames;
        }

        private async Task RemoveMemberRoles(string id, Unit unit) {
            Dictionary<string, string> roles = unit.roles;
            int originalCount = unit.roles.Count;
            foreach ((string key, string _) in roles.Where(x => x.Value == id).ToList()) {
                roles.Remove(key);
            }

            if (roles.Count != originalCount) {
                await data.Update(unit.id, Builders<Unit>.Update.Set(x => x.roles, roles));
            }
        }
    }
}