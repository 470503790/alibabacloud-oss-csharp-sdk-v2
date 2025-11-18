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
    /// A paginator for ListBuckets
    /// </summary>
    internal sealed class ListBucketsPaginator : IPaginator<ListBucketsResult>
    {
        private readonly Client _client;
        private readonly ListBucketsRequest _request;
        private int _isPaginatorInUse = 0;

        internal ListBucketsPaginator(Client client, ListBucketsRequest request, PaginatorOptions? options)
        {
            _client = client;
            _request = request;

            if (options?.Limit != null) _request.MaxKeys = options.Limit;
        }

        /// <summary>
        /// Iterates over the buckets.
        /// </summary>
        public IEnumerable<ListBucketsResult> IterPage()
        {
            if (System.Threading.Interlocked.Exchange(ref _isPaginatorInUse, 1) != 0)
                throw new InvalidOperationException(
                    "Paginator has already been consumed and cannot be reused. Please create a new instance."
                );
            var marker = _request.Marker;
            ListBucketsResult result;

            do
            {
                _request.Marker = marker;
                result = _client.ListBucketsAsync(_request).GetAwaiter().GetResult();
                marker = result.NextMarker;
                yield return result;
            } while (result.IsTruncated ?? false);
        }

#if !NET48 && !NET471 && !NETSTANDARD2_0
        /// <summary>
        /// Iterates over the buckets asynchronously.
        /// </summary>
        public async IAsyncEnumerable<ListBucketsResult> IterPageAsync([
            EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (System.Threading.Interlocked.Exchange(ref _isPaginatorInUse, 1) != 0)
                throw new InvalidOperationException(
                    "Paginator has already been consumed and cannot be reused. Please create a new instance."
                );
            var marker = _request.Marker;
            ListBucketsResult result;

            do
            {
                _request.Marker = marker;
                result = await _client.ListBucketsAsync(_request, null, cancellationToken).ConfigureAwait(false);
                marker = result.NextMarker;
                yield return result;
            } while (result.IsTruncated ?? false);
        }
#endif
    }
}
