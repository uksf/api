using UKSF.Api.ArmaMissions.Services;

namespace UKSF.Api.ArmaMissions.Models.Sqm;

public class SqmDocument
{
    public string Text { get; init; } = "";
    public SqmClassNode Root { get; init; } = new();
    public SqmClassNode EntitiesNode { get; init; }
    public List<SqmEntity> Entities { get; set; } = [];
}
