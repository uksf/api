namespace UKSF.Api.ArmaServer.Models.Persistence;

public class PlayerRedeployData
{
    public double[] Position { get; set; } = [];
    public object[] VehicleState { get; set; } = [];
    public double Direction { get; set; }
    public string Animation { get; set; } = string.Empty;
    public object[] Loadout { get; set; } = [];
    public double Damage { get; set; }
    public object[] AceMedical { get; set; } = [];
    public bool Earplugs { get; set; }
    public string[] AttachedItems { get; set; } = [];
    public object[] Radios { get; set; } = [];
    public object[] DiveState { get; set; } = [];
}
