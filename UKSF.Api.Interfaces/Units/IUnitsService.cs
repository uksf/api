﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Models.Personnel;
using UKSF.Api.Models.Units;

namespace UKSF.Api.Interfaces.Units {
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
}