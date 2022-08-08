using MongoDB.Bson;
using UKSF.Api.Base.Models;
using UKSF.Api.Personnel.Context;
using UKSF.Api.Personnel.Models;

namespace UKSF.Api.Personnel.Queries;

public interface IGetUnitTreeQuery
{
    Task<DomainUnit> ExecuteAsync(GetUnitTreeQueryArgs args);
}

public class GetUnitTreeQueryArgs
{
    public GetUnitTreeQueryArgs(UnitBranch unitBranch)
    {
        UnitBranch = unitBranch;
    }

    public UnitBranch UnitBranch { get; }
}

public class GetUnitTreeQuery : IGetUnitTreeQuery
{
    private readonly IUnitsContext _unitsContext;

    public GetUnitTreeQuery(IUnitsContext unitsContext)
    {
        _unitsContext = unitsContext;
    }

    public async Task<DomainUnit> ExecuteAsync(GetUnitTreeQueryArgs args)
    {
        var root = _unitsContext.GetSingle(x => x.Parent == ObjectId.Empty.ToString() && x.Branch == args.UnitBranch);

        root.Children = GetUnitChildren(root);

        return await Task.FromResult(root);
    }

    private List<DomainUnit> GetUnitChildren(MongoObject parentUnit)
    {
        return _unitsContext.Get(x => x.Parent == parentUnit.Id)
                            .Select(
                                x =>
                                {
                                    x.Children = GetUnitChildren(x);
                                    return x;
                                }
                            )
                            .ToList();
    }
}
