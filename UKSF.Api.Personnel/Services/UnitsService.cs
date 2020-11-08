using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver;
using UKSF.Api.Base.Context;
using UKSF.Api.Personnel.Context;
using UKSF.Api.Personnel.Models;

namespace UKSF.Api.Personnel.Services {
    public interface IUnitsService : IDataBackedService<IUnitsDataService> {
        IEnumerable<Unit> GetSortedUnits(Func<Unit, bool> predicate = null);
        Task AddMember(string id, string unitId);
        Task RemoveMember(string id, string unitName);
        Task RemoveMember(string id, Unit unit);
        Task SetMemberRole(string id, string unitId, string role = "");
        Task SetMemberRole(string id, Unit unit, string role = "");
        Task RenameRole(string oldName, string newName);
        Task DeleteRole(string role);

        bool HasRole(string unitId, string role);
        bool HasRole(Unit unit, string role);
        bool RolesHasMember(string unitId, string id);
        bool RolesHasMember(Unit unit, string id);
        bool MemberHasRole(string id, string unitId, string role);
        bool MemberHasRole(string id, Unit unit, string role);
        bool MemberHasAnyRole(string id);
        int GetMemberRoleOrder(Account account, Unit unit);

        Unit GetRoot();
        Unit GetAuxilliaryRoot();
        Unit GetParent(Unit unit);
        IEnumerable<Unit> GetParents(Unit unit);
        IEnumerable<Unit> GetChildren(Unit parent);
        IEnumerable<Unit> GetAllChildren(Unit parent, bool includeParent = false);

        int GetUnitDepth(Unit unit);
        string GetChainString(Unit unit);
    }

    public class UnitsService : DataBackedService<IUnitsDataService>, IUnitsService {
        private readonly IRolesService rolesService;

        public UnitsService(IUnitsDataService data, IRolesService rolesService) : base(data) => this.rolesService = rolesService;

        public IEnumerable<Unit> GetSortedUnits(Func<Unit, bool> predicate = null) {
            List<Unit> sortedUnits = new List<Unit>();
            Unit combatRoot = Data.GetSingle(x => x.parent == ObjectId.Empty.ToString() && x.branch == UnitBranch.COMBAT);
            Unit auxiliaryRoot = Data.GetSingle(x => x.parent == ObjectId.Empty.ToString() && x.branch == UnitBranch.AUXILIARY);
            sortedUnits.Add(combatRoot);
            sortedUnits.AddRange(GetAllChildren(combatRoot));
            sortedUnits.Add(auxiliaryRoot);
            sortedUnits.AddRange(GetAllChildren(auxiliaryRoot));

            return predicate != null ? sortedUnits.Where(predicate) : sortedUnits;
        }

        public async Task AddMember(string id, string unitId) {
            if (Data.GetSingle(x => x.id == unitId && x.members.Contains(id)) != null) return;
            await Data.Update(unitId, Builders<Unit>.Update.Push(x => x.members, id));
        }

        public async Task RemoveMember(string id, string unitName) {
            Unit unit = Data.GetSingle(x => x.name == unitName);
            if (unit == null) return;

            await RemoveMember(id, unit);
        }

        public async Task RemoveMember(string id, Unit unit) {
            if (unit.members.Contains(id)) {
                await Data.Update(unit.id, Builders<Unit>.Update.Pull(x => x.members, id));
            }

            await RemoveMemberRoles(id, unit);
        }

        public async Task SetMemberRole(string id, string unitId, string role = "") {
            Unit unit = Data.GetSingle(x => x.id == unitId);
            if (unit == null) return;

            await SetMemberRole(id, unit, role);
        }

        public async Task SetMemberRole(string id, Unit unit, string role = "") {
            await RemoveMemberRoles(id, unit);
            if (!string.IsNullOrEmpty(role)) {
                await Data.Update(unit.id, Builders<Unit>.Update.Set($"roles.{role}", id));
            }
        }

        public async Task RenameRole(string oldName, string newName) {
            foreach (Unit unit in Data.Get(x => x.roles.ContainsKey(oldName))) {
                string id = unit.roles[oldName];
                await Data.Update(unit.id, Builders<Unit>.Update.Unset($"roles.{oldName}"));
                await Data.Update(unit.id, Builders<Unit>.Update.Set($"roles.{newName}", id));
            }
        }

        public async Task DeleteRole(string role) {
            foreach (Unit unit in from unit in Data.Get(x => x.roles.ContainsKey(role)) let id = unit.roles[role] select unit) {
                await Data.Update(unit.id, Builders<Unit>.Update.Unset($"roles.{role}"));
            }
        }

        public bool HasRole(string unitId, string role) {
            Unit unit = Data.GetSingle(x => x.id == unitId);
            return HasRole(unit, role);
        }

        public bool HasRole(Unit unit, string role) => unit.roles.ContainsKey(role);

        public bool RolesHasMember(string unitId, string id) {
            Unit unit = Data.GetSingle(x => x.id == unitId);
            return RolesHasMember(unit, id);
        }

        public bool RolesHasMember(Unit unit, string id) => unit.roles.ContainsValue(id);

        public bool MemberHasRole(string id, string unitId, string role) {
            Unit unit = Data.GetSingle(x => x.id == unitId);
            return MemberHasRole(id, unit, role);
        }

        public bool MemberHasRole(string id, Unit unit, string role) => unit.roles.GetValueOrDefault(role, string.Empty) == id;

        public bool MemberHasAnyRole(string id) => Data.Get().Any(x => RolesHasMember(x, id));

        public int GetMemberRoleOrder(Account account, Unit unit) {
            if (RolesHasMember(unit, account.id)) {
                return int.MaxValue - rolesService.Data.GetSingle(x => x.name == unit.roles.FirstOrDefault(y => y.Value == account.id).Key).order;
            }

            return -1;
        }

        public Unit GetRoot() => Data.GetSingle(x => x.parent == ObjectId.Empty.ToString() && x.branch == UnitBranch.COMBAT);

        public Unit GetAuxilliaryRoot() => Data.GetSingle(x => x.parent == ObjectId.Empty.ToString() && x.branch == UnitBranch.AUXILIARY);

        public Unit GetParent(Unit unit) {
            return unit.parent != string.Empty ? Data.GetSingle(x => x.id == unit.parent) : null;
        }

        // TODO: Change this to not add the child unit to the return
        public IEnumerable<Unit> GetParents(Unit unit) {
            if (unit == null) return new List<Unit>();
            List<Unit> parentUnits = new List<Unit>();
            do {
                parentUnits.Add(unit);
                Unit child = unit;
                unit = !string.IsNullOrEmpty(unit.parent) ? Data.GetSingle(x => x.id == child.parent) : null;
                if (unit == child) break;
            } while (unit != null);

            return parentUnits;
        }

        public IEnumerable<Unit> GetChildren(Unit parent) => Data.Get(x => x.parent == parent.id).ToList();

        public IEnumerable<Unit> GetAllChildren(Unit parent, bool includeParent = false) {
            List<Unit> children = includeParent ? new List<Unit> {parent} : new List<Unit>();
            foreach (Unit unit in Data.Get(x => x.parent == parent.id)) {
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
            Unit parent = Data.GetSingle(unit.parent);
            while (parent != null) {
                depth++;
                parent = Data.GetSingle(parent.parent);
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
                await Data.Update(unit.id, Builders<Unit>.Update.Set(x => x.roles, roles));
            }
        }
    }
}
