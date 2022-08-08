namespace UKSF.Api.Base.Models;

public class PagedResult<T>
{
    public IEnumerable<T> Data { get; set; }
    public int TotalCount { get; set; }

    public PagedResult(int totalCount, IEnumerable<T> data)
    {
        TotalCount = totalCount;
        Data = data;
    }
}
