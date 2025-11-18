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
    /// A paginator for ListObjectVersions
    /// </summary>
    internal sealed class ListObjectVersionsPaginator : IPaginator<ListObjectVersionsResult>
    {
        private readonly Client _client;
        private readonly ListObjectVersionsRequest _request;
        private int _isPaginatorInUse = 0;

        internal ListObjectVersionsPaginator(Client client, ListObjectVersionsRequest request, PaginatorOptions? options)
        {
            _client = client;
            _request = request;

            if (options?.Limit != null) _request.MaxKeys = options.Limit;
        }

        /// <summary>
        /// Iterates over the object versions.
        /// </summary>
        public IEnumerable<ListObjectVersionsResult> IterPage()
        {
            if (System.Threading.Interlocked.Exchange(ref _isPaginatorInUse, 1) != 0)
                throw new InvalidOperationException(
                    "Paginator has already been consumed and cannot be reused. Please create a new instance."
                );
            var keyMarker = _request.KeyMarker;
            var versionIdMarker = _request.VersionIdMarker;
            ListObjectVersionsResult result;

            do
            {
                _request.KeyMarker = keyMarker;
                _request.VersionIdMarker = versionIdMarker;
                result = _client.ListObjectVersionsAsync(_request).GetAwaiter().GetResult();
                keyMarker = result.NextKeyMarker;
                versionIdMarker = result.NextVersionIdMarker;
                yield return result;
            } while (result.IsTruncated ?? false);
        }

#if !NET48 && !NET471 && !NETSTANDARD2_0
        /// <summary>
        /// Iterates over the object versions asynchronously.
        /// </summary>
        public async IAsyncEnumerable<ListObjectVersionsResult> IterPageAsync([
            EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (System.Threading.Interlocked.Exchange(ref _isPaginatorInUse, 1) != 0)
                throw new InvalidOperationException(
                    "Paginator has already been consumed and cannot be reused. Please create a new instance."
                );
            var keyMarker = _request.KeyMarker;
            var versionIdMarker = _request.VersionIdMarker;
            ListObjectVersionsResult result;

            do
            {
                _request.KeyMarker = keyMarker;
                _request.VersionIdMarker = versionIdMarker;
                result = await _client.ListObjectVersionsAsync(_request, null, cancellationToken).ConfigureAwait(false);
                keyMarker = result.NextKeyMarker;
                versionIdMarker = result.NextVersionIdMarker;
                yield return result;
            } while (result.IsTruncated ?? false);
        }
#endif
    }
}
