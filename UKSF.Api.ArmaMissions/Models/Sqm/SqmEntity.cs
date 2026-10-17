using UKSF.Api.ArmaMissions.Services;

namespace UKSF.Api.ArmaMissions.Models.Sqm;

public abstract class SqmEntity
{
    public SqmClassNode Node { get; init; }
    public List<string> Lines { get; init; } = [];
}

public class SqmGroup : SqmEntity
{
    public List<SqmEntity> Children { get; set; } = [];
    public bool AllChildrenPlayable { get; set; }
    public bool IsIgnored { get; set; }
}

public class SqmObject : SqmEntity
{
    public bool IsPlayable { get; set; }
    public string Type { get; set; }
}

public class SqmLogic : SqmEntity
{
    public string Type { get; set; }
}

public class SqmPassthrough : SqmEntity;
