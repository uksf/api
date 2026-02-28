using System;
using System.Collections.Generic;
using System.Linq;
using MongoDB.Bson;
using Moq;
using UKSF.Api.ArmaMissions.Services;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Services;

namespace UKSF.Api.ArmaMissions.Tests.Helpers;

public class TestPatchDataBuilder
{
    // Rank definitions (in order)
    public DomainRank RankRecruit { get; }
    public DomainRank RankPrivate { get; }
    public DomainRank RankCorporal { get; }
    public DomainRank RankSergeant { get; }
    public DomainRank RankWo2 { get; }
    public DomainRank RankMajor { get; }

    // Unit definitions
    public DomainUnit UnitUksf { get; }
    public DomainUnit UnitSfsg { get; }
    public DomainUnit UnitGuardian11 { get; } // Kestrel
    public DomainUnit UnitGuardian12 { get; } // Raider
    public DomainUnit UnitGuardian13 { get; } // Claymore
    public DomainUnit UnitGuardian1R { get; } // Reserves
    public DomainUnit UnitJsfaw { get; }
    public DomainUnit UnitCombatReady { get; }
    public DomainUnit UnitRafCranwell { get; }
    public DomainUnit UnitSniperPlatoon { get; }
    public DomainUnit UnitMedicalRegiment { get; }
    public DomainUnit UnitEmpty { get; }

    // Account definitions
    public DomainAccount MajSmith { get; }
    public DomainAccount Wo2Jones { get; }
    public DomainAccount SgtAlpha { get; }
    public DomainAccount CplBravo { get; }
    public DomainAccount PteCharlie { get; }
    public DomainAccount PilotDelta { get; }
    public DomainAccount PilotEcho { get; }
    public DomainAccount PilotFoxtrot { get; }
    public DomainAccount SniperGolf { get; }
    public DomainAccount MedicHotel { get; }

    private readonly List<DomainRank> _ranks;
    private readonly List<DomainAccount> _accounts;
    private readonly List<DomainUnit> _units;

    public TestPatchDataBuilder()
    {
        // Ranks (ordered by Order property)
        RankRecruit = new DomainRank
        {
            Id = ObjectId.GenerateNewId().ToString(),
            Name = "Recruit",
            Abbreviation = "Rec",
            Order = 0
        };
        RankPrivate = new DomainRank
        {
            Id = ObjectId.GenerateNewId().ToString(),
            Name = "Private",
            Abbreviation = "Pte",
            Order = 1
        };
        RankCorporal = new DomainRank
        {
            Id = ObjectId.GenerateNewId().ToString(),
            Name = "Corporal",
            Abbreviation = "Cpl",
            Order = 2
        };
        RankSergeant = new DomainRank
        {
            Id = ObjectId.GenerateNewId().ToString(),
            Name = "Sergeant",
            Abbreviation = "Sgt",
            Order = 3
        };
        RankWo2 = new DomainRank
        {
            Id = ObjectId.GenerateNewId().ToString(),
            Name = "WO2",
            Abbreviation = "WO2",
            Order = 4
        };
        RankMajor = new DomainRank
        {
            Id = ObjectId.GenerateNewId().ToString(),
            Name = "Major",
            Abbreviation = "Maj",
            Order = 5
        };
        _ranks = [RankRecruit, RankPrivate, RankCorporal, RankSergeant, RankWo2, RankMajor];

        // Accounts (IDs generated, will be wired into units)
        MajSmith = CreateAccount("Smith", "John", "Major", "SFSG", qualifications: new Qualifications { Engineer = true });
        Wo2Jones = CreateAccount("Jones", "Dave", "WO2", "SFSG", qualifications: new Qualifications { Engineer = true });
        SgtAlpha = CreateAccount("Alpha", "Tom", "Sergeant", "Guardian 1-1");
        CplBravo = CreateAccount("Bravo", "Jim", "Corporal", "Guardian 1-1", qualifications: new Qualifications { Medic = true });
        PteCharlie = CreateAccount("Charlie", "Ben", "Private", "Guardian 1-1");
        PilotDelta = CreateAccount("Delta", "Sam", "Corporal", "Joint Special Forces Aviation Wing");
        PilotEcho = CreateAccount("Echo", "Dan", "Private", "Joint Special Forces Aviation Wing");
        PilotFoxtrot = CreateAccount("Foxtrot", "Max", "Private", "Combat Ready");
        SniperGolf = CreateAccount("Golf", "Ash", "Corporal", "Sniper Platoon");
        MedicHotel = CreateAccount("Hotel", "Ray", "Corporal", "3 Medical Regiment");
        _accounts = [MajSmith, Wo2Jones, SgtAlpha, CplBravo, PteCharlie, PilotDelta, PilotEcho, PilotFoxtrot, SniperGolf, MedicHotel];

        // Units with MissionPatchSettings for data-driven behaviour
        UnitUksf = CreateUnit("5a42835b55d6109bf0b081bd", "UKSF", ObjectId.Empty.ToString(), "UKSF", order: 0);
        UnitSfsg = CreateUnit(
            ObjectId.GenerateNewId().ToString(),
            "SFSG",
            UnitUksf.Id,
            "Guardian",
            order: 0,
            chainOfCommand: new ChainOfCommand { First = MajSmith.Id, Nco = Wo2Jones.Id },
            members: [MajSmith.Id, Wo2Jones.Id]
        );
        UnitGuardian11 = CreateUnit(
            "5bbbb9645eb3a4170c488b36",
            "Guardian 1-1",
            UnitSfsg.Id,
            "Kestrel",
            order: 0,
            members: [SgtAlpha.Id, CplBravo.Id, PteCharlie.Id],
            chainOfCommand: new ChainOfCommand { First = SgtAlpha.Id },
            missionPatchSettings: new MissionPatchSettings
            {
                MaxSlots = 12,
                FillerName = "Reserve",
                FillerRank = "Recruit",
                IsPermanent = true
            }
        );
        UnitGuardian12 = CreateUnit(
            "5bbbbdab5eb3a4170c488f2e",
            "Guardian 1-2",
            UnitSfsg.Id,
            "Raider",
            order: 1,
            missionPatchSettings: new MissionPatchSettings
            {
                MaxSlots = 12,
                FillerName = "Reserve",
                FillerRank = "Recruit",
                IsPermanent = true
            }
        );
        UnitGuardian13 = CreateUnit(
            "5bbbbe365eb3a4170c488f30",
            "Guardian 1-3",
            UnitSfsg.Id,
            "Claymore",
            order: 2,
            missionPatchSettings: new MissionPatchSettings
            {
                MaxSlots = 12,
                FillerName = "Reserve",
                FillerRank = "Recruit",
                IsPermanent = true
            }
        );
        UnitGuardian1R = CreateUnit(
            "5ad748e0de5d414f4c4055e0",
            "Guardian 1-R",
            UnitSfsg.Id,
            "Reserves",
            order: 3,
            missionPatchSettings: new MissionPatchSettings
            {
                MaxSlots = 10,
                FillerName = "Reserve",
                FillerRank = "Recruit",
                IsPermanent = true
            }
        );
        UnitJsfaw = CreateUnit(
            "5a435eea905d47336442c75a",
            "Joint Special Forces Aviation Wing",
            UnitUksf.Id,
            "JSFAW",
            order: 1,
            members: [PilotDelta.Id, PilotEcho.Id],
            missionPatchSettings: new MissionPatchSettings { IsPilotUnit = true }
        );
        UnitCombatReady = CreateUnit(
            "5fe39de7815f5f03801134f7",
            "Combat Ready",
            UnitJsfaw.Id,
            "Combat Ready",
            order: 0,
            members: [PilotFoxtrot.Id],
            missionPatchSettings: new MissionPatchSettings
            {
                AggregateIntoParent = true,
                Pruned = true,
                IsPilotUnit = true
            }
        );
        UnitRafCranwell = CreateUnit(
            "5a848590eab14d12cc7fa618",
            "RAF Cranwell",
            UnitJsfaw.Id,
            "RAF Cranwell",
            order: 1,
            missionPatchSettings: new MissionPatchSettings { Pruned = true, IsPilotUnit = true }
        );
        UnitSniperPlatoon = CreateUnit(
            "5a68b28e196530164c9b4fed",
            "Sniper Platoon",
            UnitUksf.Id,
            "Sniper Platoon",
            order: 2,
            members: [SniperGolf.Id],
            missionPatchSettings: new MissionPatchSettings
            {
                MaxSlots = 3,
                FillerName = "Sniper",
                FillerRank = "Private",
                ForcedObjectClass = "UKSF_B_Sniper"
            }
        );
        UnitMedicalRegiment = CreateUnit(
            "5b9123ca7a6c1f0e9875601c",
            "3 Medical Regiment",
            UnitUksf.Id,
            "3 Medical Regiment",
            order: 3,
            members: [MedicHotel.Id],
            missionPatchSettings: new MissionPatchSettings { ForcedObjectClass = "UKSF_B_Medic" }
        );
        UnitEmpty = CreateUnit(ObjectId.GenerateNewId().ToString(), "Empty Unit", UnitUksf.Id, "", order: 4);

        _units =
        [
            UnitUksf, UnitSfsg, UnitGuardian11, UnitGuardian12, UnitGuardian13, UnitGuardian1R,
            UnitJsfaw, UnitCombatReady, UnitRafCranwell, UnitSniperPlatoon, UnitMedicalRegiment, UnitEmpty
        ];
    }

    public PatchDataBuilder BuildPatchDataBuilder()
    {
        return new PatchDataBuilder(
            BuildRanksContext().Object,
            BuildAccountContext().Object,
            BuildUnitsContext().Object,
            BuildRanksService().Object,
            BuildDisplayNameService().Object
        );
    }

    private Mock<IRanksContext> BuildRanksContext()
    {
        var ranksContext = new Mock<IRanksContext>();
        ranksContext.Setup(x => x.Get()).Returns(_ranks);
        ranksContext.Setup(x => x.GetSingle(It.IsAny<string>()))
                    .Returns<string>(nameOrId => _ranks.FirstOrDefault(r => r.Id == nameOrId || r.Name == nameOrId));
        return ranksContext;
    }

    private Mock<IAccountContext> BuildAccountContext()
    {
        var accountContext = new Mock<IAccountContext>();
        accountContext.Setup(x => x.Get()).Returns(_accounts);
        return accountContext;
    }

    private Mock<IUnitsContext> BuildUnitsContext()
    {
        var unitsContext = new Mock<IUnitsContext>();
        unitsContext.Setup(x => x.Get(It.IsAny<Func<DomainUnit, bool>>())).Returns<Func<DomainUnit, bool>>(predicate => _units.Where(predicate));
        return unitsContext;
    }

    private Mock<IRanksService> BuildRanksService()
    {
        var ranksService = new Mock<IRanksService>();
        ranksService.Setup(x => x.IsSuperiorOrEqual(It.IsAny<string>(), It.IsAny<string>()))
                    .Returns<string, string>((a, b) =>
                        {
                            var rankA = _ranks.FirstOrDefault(r => r.Name == a);
                            var rankB = _ranks.FirstOrDefault(r => r.Name == b);
                            if (rankA == null || rankB == null) return false;
                            return rankA.Order >= rankB.Order;
                        }
                    );
        return ranksService;
    }

    private Mock<IDisplayNameService> BuildDisplayNameService()
    {
        var displayNameService = new Mock<IDisplayNameService>();
        displayNameService.Setup(x => x.GetDisplayName(It.IsAny<DomainAccount>()))
                          .Returns<DomainAccount>(a =>
                              {
                                  var rank = _ranks.FirstOrDefault(r => r.Name == a.Rank);
                                  return $"{rank?.Abbreviation ?? a.Rank}. {a.Lastname}.{a.Firstname[0]}";
                              }
                          );
        return displayNameService;
    }

    private static DomainAccount CreateAccount(
        string lastname,
        string firstname,
        string rank,
        string unitAssignment,
        string roleAssignment = null,
        Qualifications qualifications = null
    )
    {
        return new DomainAccount
        {
            Id = ObjectId.GenerateNewId().ToString(),
            Firstname = firstname,
            Lastname = lastname,
            Rank = rank,
            UnitAssignment = unitAssignment,
            RoleAssignment = roleAssignment,
            Qualifications = qualifications ?? new Qualifications(),
            MembershipState = MembershipState.Member
        };
    }

    private static DomainUnit CreateUnit(
        string id,
        string name,
        string parent,
        string callsign,
        int order = 0,
        List<string> members = null,
        ChainOfCommand chainOfCommand = null,
        MissionPatchSettings missionPatchSettings = null
    )
    {
        return new DomainUnit
        {
            Id = id,
            Name = name,
            Parent = parent,
            Callsign = callsign,
            Branch = UnitBranch.Combat,
            Order = order,
            Members = members ?? [],
            ChainOfCommand = chainOfCommand ?? new ChainOfCommand(),
            MissionPatchSettings = missionPatchSettings
        };
    }
}
