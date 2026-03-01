namespace UKSF.Api.ArmaServer.Models.Persistence;

public class PersistenceObject
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public double[] Position { get; set; } = [];
    public double[][] VectorDirUp { get; set; } = [];
    public double Damage { get; set; }
    public double Fuel { get; set; }
    public object[] TurretWeapons { get; set; } = [];
    public object[] TurretMagazines { get; set; } = [];
    public object[] PylonLoadout { get; set; } = [];
    public double[] Logistics { get; set; } = [];
    public object[] Attached { get; set; } = [];
    public object[] RackChannels { get; set; } = [];
    public object[] AceCargo { get; set; } = [];
    public object[][] Inventory { get; set; } = [];
    public object[] AceFortify { get; set; } = [];
    public object[] AceMedical { get; set; } = [];
    public object[] AceRepair { get; set; } = [];
    public string CustomName { get; set; } = string.Empty;
}
