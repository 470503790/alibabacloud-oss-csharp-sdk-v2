using System.Collections.Generic;
using System.Threading;

namespace AlibabaCloud.OSS.V2.Paginator
{
    /// <summary>
    /// Interface for operation paginators
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public interface IPaginator<out T>
    {
        IEnumerable<T> IterPage();
#if !NET48 && !NET471 && !NETSTANDARD2_0
        IAsyncEnumerable<T> IterPageAsync(CancellationToken cancellationToken = default);
#endif
    }
}
