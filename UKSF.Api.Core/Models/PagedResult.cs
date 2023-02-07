namespace UKSF.Api.Core.Models;

public class PagedResult<T>
{
    public PagedResult(int totalCount, IEnumerable<T> data)
    {
        TotalCount = totalCount;
        Data = data;
    }

    public IEnumerable<T> Data { get; set; }
    public int TotalCount { get; set; }
}
