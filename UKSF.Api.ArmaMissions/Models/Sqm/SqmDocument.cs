namespace UKSF.Api.ArmaMissions.Models.Sqm;

public class SqmDocument
{
    public List<string> HeaderLines { get; set; } = [];
    public List<SqmEntity> Entities { get; set; } = [];
    public List<string> FooterLines { get; set; } = [];
}
