namespace UKSF.Api.Core.Models;

public class PagedResult<T>(int totalCount, IEnumerable<T> data)
{
    public IEnumerable<T> Data { get; set; } = data;
    public int TotalCount { get; set; } = totalCount;
}
