using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver;
using UKSF.Api.Personnel.Context;
using UKSF.Api.Personnel.Models;

namespace UKSF.Api.Personnel.Services
{
    public interface IUnitsService
    {
        IEnumerable<DomainUnit> GetSortedUnits(Func<DomainUnit, bool> predicate = null);
        Task AddMember(string id, string unitId);
        Task RemoveMember(string id, string unitName);
        Task RemoveMember(string id, DomainUnit unit);
        Task SetMemberRole(string id, string unitId, string role = "");
        Task SetMemberRole(string id, DomainUnit unit, string role = "");
        Task RenameRole(string oldName, string newName);
        Task DeleteRole(string role);

        bool HasRole(string unitId, string role);
        bool HasRole(DomainUnit unit, string role);
        bool RolesHasMember(string unitId, string id);
        bool RolesHasMember(DomainUnit unit, string id);
        bool MemberHasRole(string id, string unitId, string role);
        bool MemberHasRole(string id, DomainUnit unit, string role);
        bool MemberHasAnyRole(string id);
        int GetMemberRoleOrder(DomainAccount domainAccount, DomainUnit unit);

        DomainUnit GetRoot();
        DomainUnit GetAuxilliaryRoot();
        DomainUnit GetParent(DomainUnit unit);
        IEnumerable<DomainUnit> GetParents(DomainUnit unit);
        IEnumerable<DomainUnit> GetChildren(DomainUnit parent);
        IEnumerable<DomainUnit> GetAllChildren(DomainUnit parent, bool includeParent = false);

        int GetUnitDepth(DomainUnit unit);
        string GetChainString(DomainUnit unit);
    }

    public class UnitsService : IUnitsService
    {
        private readonly IRolesContext _rolesContext;
        private readonly IUnitsContext _unitsContext;

        public UnitsService(IUnitsContext unitsContext, IRolesContext rolesContext)
        {
            _unitsContext = unitsContext;
            _rolesContext = rolesContext;
        }

        public IEnumerable<DomainUnit> GetSortedUnits(Func<DomainUnit, bool> predicate = null)
        {
            List<DomainUnit> sortedUnits = new();
            var combatRoot = _unitsContext.GetSingle(x => x.Parent == ObjectId.Empty.ToString() && x.Branch == UnitBranch.COMBAT);
            var auxiliaryRoot = _unitsContext.GetSingle(x => x.Parent == ObjectId.Empty.ToString() && x.Branch == UnitBranch.AUXILIARY);
            sortedUnits.Add(combatRoot);
            sortedUnits.AddRange(GetAllChildren(combatRoot));
            sortedUnits.Add(auxiliaryRoot);
            sortedUnits.AddRange(GetAllChildren(auxiliaryRoot));

            return predicate != null ? sortedUnits.Where(predicate) : sortedUnits;
        }

        public async Task AddMember(string id, string unitId)
        {
            if (_unitsContext.GetSingle(x => x.Id == unitId && x.Members.Contains(id)) != null)
            {
                return;
            }

            await _unitsContext.Update(unitId, Builders<DomainUnit>.Update.Push(x => x.Members, id));
        }

        public async Task RemoveMember(string id, string unitName)
        {
            var unit = _unitsContext.GetSingle(x => x.Name == unitName);
            if (unit == null)
            {
                return;
            }

            await RemoveMember(id, unit);
        }

        public async Task RemoveMember(string id, DomainUnit unit)
        {
            if (unit.Members.Contains(id))
            {
                await _unitsContext.Update(unit.Id, Builders<DomainUnit>.Update.Pull(x => x.Members, id));
            }

            await RemoveMemberRoles(id, unit);
        }

        public async Task SetMemberRole(string id, string unitId, string role = "")
        {
            var unit = _unitsContext.GetSingle(x => x.Id == unitId);
            if (unit == null)
            {
                return;
            }

            await SetMemberRole(id, unit, role);
        }

        public async Task SetMemberRole(string id, DomainUnit unit, string role = "")
        {
            await RemoveMemberRoles(id, unit);
            if (!string.IsNullOrEmpty(role))
            {
                await _unitsContext.Update(unit.Id, Builders<DomainUnit>.Update.Set($"roles.{role}", id));
            }
        }

        public async Task RenameRole(string oldName, string newName)
        {
            foreach (var unit in _unitsContext.Get(x => x.Roles.ContainsKey(oldName)))
            {
                var id = unit.Roles[oldName];
                await _unitsContext.Update(unit.Id, Builders<DomainUnit>.Update.Unset($"roles.{oldName}"));
                await _unitsContext.Update(unit.Id, Builders<DomainUnit>.Update.Set($"roles.{newName}", id));
            }
        }

        public async Task DeleteRole(string role)
        {
            foreach (var unit in from unit in _unitsContext.Get(x => x.Roles.ContainsKey(role)) let id = unit.Roles[role] select unit)
            {
                await _unitsContext.Update(unit.Id, Builders<DomainUnit>.Update.Unset($"roles.{role}"));
            }
        }

        public bool HasRole(string unitId, string role)
        {
            var unit = _unitsContext.GetSingle(x => x.Id == unitId);
            return HasRole(unit, role);
        }

        public bool HasRole(DomainUnit unit, string role)
        {
            return unit.Roles.ContainsKey(role);
        }

        public bool RolesHasMember(string unitId, string id)
        {
            var unit = _unitsContext.GetSingle(x => x.Id == unitId);
            return RolesHasMember(unit, id);
        }

        public bool RolesHasMember(DomainUnit unit, string id)
        {
            return unit.Roles.ContainsValue(id);
        }

        public bool MemberHasRole(string id, string unitId, string role)
        {
            var unit = _unitsContext.GetSingle(x => x.Id == unitId);
            return MemberHasRole(id, unit, role);
        }

        public bool MemberHasRole(string id, DomainUnit unit, string role)
        {
            return unit.Roles.GetValueOrDefault(role, string.Empty) == id;
        }

        public bool MemberHasAnyRole(string id)
        {
            return _unitsContext.Get().Any(x => RolesHasMember(x, id));
        }

        public int GetMemberRoleOrder(DomainAccount domainAccount, DomainUnit unit)
        {
            if (RolesHasMember(unit, domainAccount.Id))
            {
                return int.MaxValue - _rolesContext.GetSingle(x => x.Name == unit.Roles.FirstOrDefault(y => y.Value == domainAccount.Id).Key).Order;
            }

            return -1;
        }

        public DomainUnit GetRoot()
        {
            return _unitsContext.GetSingle(x => x.Parent == ObjectId.Empty.ToString() && x.Branch == UnitBranch.COMBAT);
        }

        public DomainUnit GetAuxilliaryRoot()
        {
            return _unitsContext.GetSingle(x => x.Parent == ObjectId.Empty.ToString() && x.Branch == UnitBranch.AUXILIARY);
        }

        public DomainUnit GetParent(DomainUnit unit)
        {
            return unit.Parent != string.Empty ? _unitsContext.GetSingle(x => x.Id == unit.Parent) : null;
        }

        // TODO: Change this to not add the child unit to the return
        public IEnumerable<DomainUnit> GetParents(DomainUnit unit)
        {
            if (unit == null)
            {
                return new List<DomainUnit>();
            }

            List<DomainUnit> parentUnits = new();
            do
            {
                parentUnits.Add(unit);
                var child = unit;
                unit = !string.IsNullOrEmpty(unit.Parent) ? _unitsContext.GetSingle(x => x.Id == child.Parent) : null;
                if (unit == child)
                {
                    break;
                }
            }
            while (unit != null);

            return parentUnits;
        }

        public IEnumerable<DomainUnit> GetChildren(DomainUnit parent)
        {
            return _unitsContext.Get(x => x.Parent == parent.Id).ToList();
        }

        public IEnumerable<DomainUnit> GetAllChildren(DomainUnit parent, bool includeParent = false)
        {
            var children = includeParent ? new() { parent } : new List<DomainUnit>();
            foreach (var unit in _unitsContext.Get(x => x.Parent == parent.Id))
            {
                children.Add(unit);
                children.AddRange(GetAllChildren(unit));
            }

            return children;
        }

        public int GetUnitDepth(DomainUnit unit)
        {
            if (unit.Parent == ObjectId.Empty.ToString())
            {
                return 0;
            }

            var depth = 0;
            var parent = _unitsContext.GetSingle(unit.Parent);
            while (parent != null)
            {
                depth++;
                parent = _unitsContext.GetSingle(parent.Parent);
            }

            return depth;
        }

        public string GetChainString(DomainUnit unit)
        {
            var parentUnits = GetParents(unit).Skip(1).ToList();
            var unitNames = unit.Name;
            parentUnits.ForEach(x => unitNames += $", {x.Name}");
            return unitNames;
        }

        private async Task RemoveMemberRoles(string id, DomainUnit unit)
        {
            var roles = unit.Roles;
            var originalCount = unit.Roles.Count;
            foreach (var (key, _) in roles.Where(x => x.Value == id).ToList())
            {
                roles.Remove(key);
            }

            if (roles.Count != originalCount)
            {
                await _unitsContext.Update(unit.Id, Builders<DomainUnit>.Update.Set(x => x.Roles, roles));
            }
        }
    }
}
