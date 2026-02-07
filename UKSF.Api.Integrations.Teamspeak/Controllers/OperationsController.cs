using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using UKSF.Api.Core;
using UKSF.Api.Integrations.Teamspeak.Models;

namespace UKSF.Api.Integrations.Teamspeak.Controllers;

[Route("[controller]")]
[Permissions(Permissions.Member)]
public class OperationsController : ControllerBase
{
    private readonly IMongoDatabase _database;

    public OperationsController(IMongoDatabase database)
    {
        _database = database;
    }

    [HttpGet]
    [Authorize]
    public TeamspeakReportsDataset Get()
    {
        var tsServerSnapshots = _database.GetCollection<TeamspeakServerSnapshot>("teamspeakSnapshots")
                                         .Find(x => x.Timestamp > DateTime.UtcNow.AddDays(-7))
                                         .ToList();
        TeamspeakReportDataset acreData = new() { Labels = GetLabels(), Datasets = GetReports(tsServerSnapshots, true) };
        TeamspeakReportDataset data = new() { Labels = GetLabels(), Datasets = GetReports(tsServerSnapshots, false) };
        return new TeamspeakReportsDataset { AcreData = acreData, Data = data };
    }

    private static int[] GetReportData(IReadOnlyCollection<TeamspeakServerSnapshot> serverSnapshots, DateTime day, bool acre)
    {
        List<int> dataset = [];
        for (var i = 0; i < 48; i++)
        {
            var startdate = DateTime.UtcNow.Date.AddMinutes(30 * i);
            var enddate = DateTime.UtcNow.Date.AddMinutes(30 * (i + 1));
            try
            {
                var serverSnapshot = serverSnapshots.FirstOrDefault(x => x.Timestamp.TimeOfDay > startdate.TimeOfDay &&
                                                                         x.Timestamp.TimeOfDay < enddate.TimeOfDay &&
                                                                         x.Timestamp.Date == day
                );
                if (serverSnapshot is not null)
                {
                    dataset.Add(acre ? serverSnapshot.Users.Where(x => x.ChannelName == "ACRE").ToArray().Length : serverSnapshot.Users.Count);
                }
                else
                {
                    dataset.Add(0);
                }
            }
            catch (InvalidOperationException)
            {
                dataset.Add(0);
            }
        }

        return dataset.ToArray();
    }

    private static List<string> GetLabels()
    {
        List<string> labels = [];

        for (var i = 0; i < 48; i++)
        {
            var startdate = DateTime.UtcNow.Date.AddMinutes(30 * i);
            var enddate = DateTime.UtcNow.Date.AddMinutes(30 * (i + 1));
            labels.Add(startdate.TimeOfDay + " - " + enddate.TimeOfDay);
        }

        return labels;
    }

    private static List<TeamspeakReport> GetReports(IReadOnlyCollection<TeamspeakServerSnapshot> tsServerSnapshots, bool acre)
    {
        List<TeamspeakReport> datasets = [];
        string[] colors = ["#4bc0c0", "#3992e6", "#a539e6", "#42e639", "#aae639", "#e6d239", "#e63939"];

        for (var i = 0; i < 7; i++)
        {
            datasets.Add(
                new TeamspeakReport
                {
                    Label = $"{DateTime.UtcNow.AddDays(-i).DayOfWeek} - {DateTime.UtcNow.AddDays(-i).ToShortDateString()}",
                    Data = GetReportData(tsServerSnapshots, DateTime.UtcNow.AddDays(-i).Date, acre),
                    Fill = true,
                    BorderColor = colors[i]
                }
            );
        }

        return datasets;
    }
}
