using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using UKSFWebsite.Api.Models.Integrations;
using UKSFWebsite.Api.Services.Personnel;

namespace UKSFWebsite.Api.Controllers.Accounts {
    [Route("[controller]"), Roles(RoleDefinitions.MEMBER)]
    public class OperationsController : Controller {
        private readonly IMongoDatabase database;

        public OperationsController(IMongoDatabase database) => this.database = database;

        [HttpGet, Authorize]
        public IActionResult Get() {
            List<TeamspeakServerSnapshot> tsServerSnapshots = database.GetCollection<TeamspeakServerSnapshot>("teamspeakSnapshots").Find(x => x.timestamp > DateTime.Now.AddDays(-7)).ToList();
            var acreData = new {labels = GetLabels(), datasets = GetDataSets(tsServerSnapshots, true)};
            var data = new {labels = GetLabels(), datasets = GetDataSets(tsServerSnapshots, false)};
            return Ok(new {acreData, data});
        }

        private static int[] GetData(IReadOnlyCollection<TeamspeakServerSnapshot> serverSnapshots, DateTime day, bool acre) {
            List<int> dataset = new List<int>();
            for (int i = 0; i < 48; i++) {
                DateTime startdate = DateTime.Today.AddMinutes(30 * i);
                DateTime enddate = DateTime.Today.AddMinutes(30 * (i + 1));
                try {
                    TeamspeakServerSnapshot serverSnapshot = serverSnapshots.FirstOrDefault(x => x.timestamp.TimeOfDay > startdate.TimeOfDay && x.timestamp.TimeOfDay < enddate.TimeOfDay && x.timestamp.Date == day);
                    if (serverSnapshot != null) {
                        dataset.Add(acre ? serverSnapshot.users.Where(x => x.channelName == "ACRE").ToArray().Length : serverSnapshot.users.Count);
                    } else {
                        dataset.Add(0);
                    }
                } catch (Exception) {
                    dataset.Add(0);
                }
            }

            return dataset.ToArray();
        }

        private static List<string> GetLabels() {
            List<string> labels = new List<string>();

            for (int i = 0; i < 48; i++) {
                DateTime startdate = DateTime.Today.AddMinutes(30 * i);
                DateTime enddate = DateTime.Today.AddMinutes(30 * (i + 1));
                labels.Add(startdate.TimeOfDay + " - " + enddate.TimeOfDay);
            }

            return labels;
        }

        private static List<object> GetDataSets(IReadOnlyCollection<TeamspeakServerSnapshot> tsServerSnapshots, bool acre) {
            List<object> datasets = new List<object>();
            string[] colors = {"#4bc0c0", "#3992e6", "#a539e6", "#42e639", "#aae639", "#e6d239", "#e63939"};

            for (int i = 0; i < 7; i++) {
                datasets.Add(new {label = $"{DateTime.Now.AddDays(-i).DayOfWeek} - {DateTime.Now.AddDays(-i).ToShortDateString()}", data = GetData(tsServerSnapshots, DateTime.Now.AddDays(-i).Date, acre), fill = true, borderColor = colors[i]});
            }

            return datasets;
        }
    }
}
