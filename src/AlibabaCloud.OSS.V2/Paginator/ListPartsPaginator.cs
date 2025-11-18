using System;
using System.Collections.Generic;
#if !NET48 && !NET471 && !NETSTANDARD2_0
using System.Runtime.CompilerServices;
using System.Threading;
#endif
using AlibabaCloud.OSS.V2.Models;

namespace AlibabaCloud.OSS.V2.Paginator
{
    /// <summary>
    /// A paginator for ListParts
    /// </summary>
    internal sealed class ListPartsPaginator : IPaginator<ListPartsResult>
    {
        private readonly Client _client;
        private readonly ListPartsRequest _request;
        private int _isPaginatorInUse = 0;

        internal ListPartsPaginator(Client client, ListPartsRequest request, PaginatorOptions? options)
        {
            _client = client;
            _request = request;

            if (options?.Limit != null) _request.MaxParts = options.Limit;
        }

        /// <summary>
        /// Iterates over the parts.
        /// </summary>
        public IEnumerable<ListPartsResult> IterPage()
        {
            if (System.Threading.Interlocked.Exchange(ref _isPaginatorInUse, 1) != 0)
                throw new InvalidOperationException(
                    "Paginator has already been consumed and cannot be reused. Please create a new instance."
                );
            var partNumberMarker = _request.PartNumberMarker;
            ListPartsResult result;

            do
            {
                _request.PartNumberMarker = partNumberMarker;
                result = _client.ListPartsAsync(_request).GetAwaiter().GetResult();
                partNumberMarker = result.NextPartNumberMarker;
                yield return result;
            } while (result.IsTruncated ?? false);
        }

#if !NET48 && !NET471 && !NETSTANDARD2_0
        /// <summary>
        /// Iterates over the parts asynchronously.
        /// </summary>
        public async IAsyncEnumerable<ListPartsResult> IterPageAsync([
            EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (System.Threading.Interlocked.Exchange(ref _isPaginatorInUse, 1) != 0)
                throw new InvalidOperationException(
                    "Paginator has already been consumed and cannot be reused. Please create a new instance."
                );
            var partNumberMarker = _request.PartNumberMarker;
            ListPartsResult result;

            do
            {
                _request.PartNumberMarker = partNumberMarker;
                result = await _client.ListPartsAsync(_request, null, cancellationToken).ConfigureAwait(false);
                partNumberMarker = result.NextPartNumberMarker;
                yield return result;
            } while (result.IsTruncated ?? false);
        }
#endif
    }
}
