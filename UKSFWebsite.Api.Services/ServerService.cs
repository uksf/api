using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UKSFWebsite.Api.Models;
using UKSFWebsite.Api.Models.Accounts;
using UKSFWebsite.Api.Services.Abstraction;
using UKSFWebsite.Api.Services.Data;
// ReSharper disable HeuristicUnreachableCode
#pragma warning disable 162

namespace UKSFWebsite.Api.Services {
    public class ServerService : IServerService {
        private const string FILE_BACKUP = "backup.xml";
        private const string FILE_SQUAD = "squad.xml";
        private const string PATH = "C:\\wamp\\www\\uksfnew\\public\\squadtag\\A3";

        private readonly IAccountService accountService;
        private readonly IDisplayNameService displayNameService;
        private readonly IRanksService ranksService;
        private readonly IUnitsService unitsService;

        public ServerService(IAccountService accountService, IRanksService ranksService, IDisplayNameService displayNameService, IUnitsService unitsService) {
            this.accountService = accountService;
            this.ranksService = ranksService;
            this.displayNameService = displayNameService;
            this.unitsService = unitsService;
        }

        public void UpdateSquadXml() {
            return;
            Task.Run(
                () => {
                    List<Account> accounts = accountService.Get(x => x.membershipState == MembershipState.MEMBER && x.rank != null);
                    accounts = accounts.OrderBy(x => x.rank, new RankComparer(ranksService)).ThenBy(x => x.lastname).ThenBy(x => x.firstname).ToList();

                    StringBuilder stringBuilder = new StringBuilder();
                    stringBuilder.AppendLine(
                        "<?xml version=\"1.0\"?>\n<!DOCTYPE squad SYSTEM \"squad.dtd\">\n<?xml-stylesheet href=\"squad.xsl?\" type=\"text/xsl\"?>\n\n<squad nick=\"=UKSF=\">\n\t<name>United Kingdom Special Forces</name>\n\t<email>uksfrecruitment@gmail.com</email>\n\t<web>https://uk-sf.co.uk</web>\n\t<picture></picture>\n\t<title>United Kingdom Special Forces</title>\n"
                    );

                    foreach (Account account in accounts.Where(x => ranksService.IsSuperiorOrEqual(x.rank, "Private"))) {
                        StringBuilder accountStringBuilder = new StringBuilder();
                        Unit unit = unitsService.GetSingle(x => x.name == account.unitAssignment);
                        string unitRole = unit.roles.FirstOrDefault(x => x.Value == account.id).Key;
                        accountStringBuilder.AppendLine($"\t<member id=\"{account.steamname}\" nick=\"{displayNameService.GetDisplayName(account)}\">");
                        accountStringBuilder.AppendLine($"\t\t<name>{unit.callsign}</name>");
                        accountStringBuilder.AppendLine($"\t\t<email>{account.rank}</email>");
                        accountStringBuilder.AppendLine($"\t\t<icq>{account.unitAssignment}{(string.IsNullOrEmpty(unitRole) ? "" : $" {unitRole}")}</icq>");
                        accountStringBuilder.AppendLine($"\t\t<remark>{account.roleAssignment}</remark>");
                        accountStringBuilder.AppendLine("\t</member>");
                        stringBuilder.AppendLine(accountStringBuilder.ToString());
                    }

                    stringBuilder.AppendLine("</squad>");

                    try {
                        File.Copy(Path.Join(PATH, FILE_SQUAD), Path.Join(PATH, FILE_BACKUP));

                        try {
                            File.WriteAllText(Path.Join(PATH, FILE_SQUAD), stringBuilder.ToString());
                        } catch (Exception) {
                            File.Delete(Path.Join(PATH, FILE_SQUAD));
                            File.Copy(Path.Join(PATH, FILE_BACKUP), Path.Join(PATH, FILE_SQUAD));
                            File.Delete(Path.Join(PATH, FILE_BACKUP));
                        }
                    } catch (Exception) {
                        // ignored
                    }
                }
            );
        }
    }
}
