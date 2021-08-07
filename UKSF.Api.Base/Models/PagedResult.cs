using System.Collections.Generic;

namespace UKSF.Api.Base.Models
{
    public class PagedResult<T>
    {
        public IEnumerable<T> Data;
        public int TotalCount;

        public PagedResult(int totalCount, IEnumerable<T> data)
        {
            TotalCount = totalCount;
            Data = data;
        }
    }
}
