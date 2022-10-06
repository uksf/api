namespace UKSF.Api.ArmaServer.Models;

public class GameServerMod
{
    public bool IsDuplicate { get; set; }
    public string Name { get; set; }
    public string Path { get; set; }
    public string PathRelativeToServerExecutable { get; set; }

    public override string ToString()
    {
        return Name;
    }
}
