﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MongoDB.Driver;
using UKSFWebsite.Api.Interfaces.Personnel;
using UKSFWebsite.Api.Interfaces.Units;
using UKSFWebsite.Api.Models.Integrations;
using UKSFWebsite.Api.Models.Personnel;

namespace UKSFWebsite.Api.Services.Personnel {
    public class AttendanceService : IAttendanceService {
        private readonly IAccountService accountService;
        private readonly IMongoDatabase database;
        private readonly IDisplayNameService displayNameService;
        private readonly ILoaService loaService;
        private readonly IUnitsService unitsService;
        private List<Account> accounts;
        private List<TeamspeakServerSnapshot> records;

        public AttendanceService(IAccountService accountService, IDisplayNameService displayNameService, ILoaService loaService, IMongoDatabase database, IUnitsService unitsService) {
            this.accountService = accountService;
            this.displayNameService = displayNameService;
            this.loaService = loaService;
            this.database = database;
            this.unitsService = unitsService;
        }

        public async Task<AttendanceReport> GenerateAttendanceReport(DateTime start, DateTime end) {
            await GetRecords(start, end);
            GetAccounts();
            AccountAttendanceStatus[] reports = accounts.Select(
                                                            x => new AccountAttendanceStatus {
                                                                accountId = x.id,
                                                                displayName = displayNameService.GetDisplayName(x),
                                                                attendancePercent = GetAttendancePercent(x.teamspeakIdentities),
                                                                attendanceState = loaService.IsLoaCovered(x.id, start) ? AttendanceState.LOA : GetAttendanceState(GetAttendancePercent(x.teamspeakIdentities)),
                                                                groupId = unitsService.Data().GetSingle(y => y.name == x.unitAssignment).id,
                                                                groupName = x.unitAssignment
                                                            }
                                                        )
                                                        .ToArray();
            return new AttendanceReport {users = reports};
        }

        private void GetAccounts() {
            accounts = accountService.Data().Get(x => x.membershipState == MembershipState.MEMBER);
        }

        private async Task GetRecords(DateTime start, DateTime end) {
            records = (await database.GetCollection<TeamspeakServerSnapshot>("teamspeakSnapshots").FindAsync(x => x.timestamp > start && x.timestamp < end)).ToList();
        }

        private float GetAttendancePercent(ICollection<string> userTsId) {
            IEnumerable<TeamspeakServerSnapshot> presentRecords = records.Where(record => record.users.Any(x => userTsId.Contains(x.clientDbId.ToString()) && x.channelName == "ACRE"));
            return presentRecords.Count() / (float) records.Count;
        }

        private static AttendanceState GetAttendanceState(float attendancePercent) => attendancePercent > 0.6 ? AttendanceState.FULL : attendancePercent > 0.3 ? AttendanceState.PARTIAL : AttendanceState.MIA;
    }
}