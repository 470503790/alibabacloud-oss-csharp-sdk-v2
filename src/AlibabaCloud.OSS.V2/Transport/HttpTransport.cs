using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
#if NETCOREAPP
using System.Net.Security;
#endif

namespace AlibabaCloud.OSS.V2.Transport
{
    /// <summary>
    /// An implementation that uses <see cref="HttpClient"/> as the transport.
    /// </summary>
    public class HttpTransport : IDisposable
    {
        /// <summary>
        /// A shared instance of <see cref="HttpTransport"/> with default parameters.
        /// </summary>
        public static readonly HttpTransport Shared = new HttpTransport();

        // The transport's private HttpClient is internal because it is used by tests.
        internal HttpClient Client { get; }
        internal bool _disposeClient = true;
        private readonly HttpTransportOptions? _options;

        /// <summary>
        /// Creates a new <see cref="HttpTransport"/> instance using default configuration.
        /// </summary>
        public HttpTransport() : this(CreateDefaultClient()) { }

        /// <summary>
        /// Creates a new <see cref="HttpTransport"/> instance using the provided options.
        /// </summary>
        /// <param name="options">The transport options to use.</param>
        public HttpTransport(HttpTransportOptions options) : this(CreateCustomClient(options))
        {
            _options = options;
        }

        /// <summary>
        /// Creates a new instance of <see cref="HttpTransport"/> using the provided client instance.
        /// </summary>
        /// <param name="messageHandler">The instance of <see cref="HttpMessageHandler"/> to use.</param>
        public HttpTransport(HttpMessageHandler messageHandler)
        {
            Client = new HttpClient(messageHandler) ?? throw new ArgumentNullException(nameof(messageHandler));
        }

        /// <summary>
        /// Creates a new instance of <see cref="HttpTransport"/> using the provided client instance.
        /// </summary>
        /// <param name="client">The instance of <see cref="HttpClient"/> to use.</param>
        /// <param name="disposeClient">true if the inner client should be disposed of by Dispose(), false if you intend
        /// to reuse the inner client.</param>
        public HttpTransport(HttpClient client, bool disposeClient = true)
        {
            Client = client ?? throw new ArgumentNullException(nameof(client));
            _disposeClient = disposeClient;
        }

        /// <summary>
        /// Send an HTTP request as an asynchronous operation.
        /// <param name="request">The HTTP request message to send.</param>
        /// <param name="completionOption">When the operation should complete (as soon as a response is available or after
        /// reading the whole response content)</param>
        /// <param name="cancellationToken">The cancellation token to cancel operation.</param>
        /// </summary>
        public Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, HttpCompletionOption completionOption, CancellationToken cancellationToken)
        {
            return Client.SendAsync(request, completionOption, cancellationToken);
        }

        /// <summary>
        /// Send an HTTP request as a synchronous operation using WebRequest.
        /// <param name="request">The HTTP request message to send.</param>
        /// <param name="completionOption">When the operation should complete (as soon as a response is available or after
        /// reading the whole response content)</param>
        /// <param name="cancellationToken">The cancellation token to cancel operation.</param>
        /// </summary>
#pragma warning disable SYSLIB0014 // WebRequest is obsolete but required by the specification
        public HttpResponseMessage Send(HttpRequestMessage request, HttpCompletionOption completionOption, CancellationToken cancellationToken)
        {
            if (request?.RequestUri == null)
            {
                throw new ArgumentNullException(nameof(request), "Request or RequestUri cannot be null");
            }

            cancellationToken.ThrowIfCancellationRequested();

            var webRequest = (HttpWebRequest)WebRequest.Create(request.RequestUri);
            webRequest.Method = request.Method.Method;

            // Apply options if available
            if (_options != null)
            {
                if (_options.ConnectTimeout.HasValue)
                {
                    webRequest.Timeout = (int)_options.ConnectTimeout.Value.TotalMilliseconds;
                }
                if (_options.EnabledRedirect.HasValue)
                {
                    webRequest.AllowAutoRedirect = _options.EnabledRedirect.Value;
                }
                if (_options.HttpProxy != null)
                {
                    webRequest.Proxy = _options.HttpProxy;
                }
#if !NETCOREAPP
                if (_options.InsecureSkipVerify.GetValueOrDefault(false))
                {
                    webRequest.ServerCertificateValidationCallback = delegate { return true; };
                }
#else
                // For .NET Core/5+, SSL validation is handled differently with WebRequest
                // ServicePointManager is not available in .NET Core
                if (_options.InsecureSkipVerify.GetValueOrDefault(false))
                {
                    // Note: WebRequest in .NET Core doesn't support per-request certificate validation
                    // This would require using HttpClient or setting a global handler
                }
#endif
            }

            // Copy headers from HttpRequestMessage to WebRequest
            foreach (var header in request.Headers)
            {
                var headerName = header.Key;
                var headerValue = string.Join(",", header.Value);

                switch (headerName.ToLowerInvariant())
                {
                    case "accept":
                        webRequest.Accept = headerValue;
                        break;
                    case "connection":
                        if (headerValue.Equals("keep-alive", StringComparison.OrdinalIgnoreCase))
                            webRequest.KeepAlive = true;
                        else if (headerValue.Equals("close", StringComparison.OrdinalIgnoreCase))
                            webRequest.KeepAlive = false;
                        break;
                    case "content-type":
                        webRequest.ContentType = headerValue;
                        break;
                    case "expect":
                        if (headerValue == "100-continue")
                            webRequest.ServicePoint.Expect100Continue = true;
                        break;
                    case "user-agent":
                        webRequest.UserAgent = headerValue;
                        break;
                    case "host":
                        webRequest.Host = headerValue;
                        break;
                    case "referer":
                        webRequest.Referer = headerValue;
                        break;
                    case "transfer-encoding":
                        if (headerValue.Equals("chunked", StringComparison.OrdinalIgnoreCase))
                            webRequest.SendChunked = true;
                        break;
                    default:
                        try
                        {
                            webRequest.Headers.Add(headerName, headerValue);
                        }
                        catch
                        {
                            // Some headers cannot be set directly
                        }
                        break;
                }
            }

            // Copy content headers if content exists
            if (request.Content != null)
            {
                foreach (var header in request.Content.Headers)
                {
                    var headerName = header.Key;
                    var headerValue = string.Join(",", header.Value);

                    switch (headerName.ToLowerInvariant())
                    {
                        case "content-type":
                            webRequest.ContentType = headerValue;
                            break;
                        case "content-length":
                            if (long.TryParse(headerValue, out var contentLength))
                                webRequest.ContentLength = contentLength;
                            break;
                        default:
                            try
                            {
                                webRequest.Headers.Add(headerName, headerValue);
                            }
                            catch
                            {
                                // Some headers cannot be set directly
                            }
                            break;
                    }
                }

                // Write request body
                if (request.Method != HttpMethod.Get && request.Method != HttpMethod.Head)
                {
                    using (var requestStream = webRequest.GetRequestStream())
                    {
                        request.Content.CopyToAsync(requestStream).GetAwaiter().GetResult();
                    }
                }
            }

            // Execute the request
            HttpWebResponse webResponse;
            try
            {
                webResponse = (HttpWebResponse)webRequest.GetResponse();
            }
            catch (WebException ex) when (ex.Response is HttpWebResponse errorResponse)
            {
                webResponse = errorResponse;
            }

            // Convert WebResponse to HttpResponseMessage
            var response = new HttpResponseMessage((HttpStatusCode)webResponse.StatusCode);
            response.ReasonPhrase = webResponse.StatusDescription;
            response.RequestMessage = request;

            // Copy response headers
            foreach (var headerName in webResponse.Headers.AllKeys)
            {
                var headerValue = webResponse.Headers[headerName];
                if (headerValue == null) continue;

                var isContentHeader = headerName.StartsWith("Content-", StringComparison.OrdinalIgnoreCase);
                
                if (isContentHeader)
                {
                    if (response.Content == null)
                    {
                        response.Content = new StreamContent(Stream.Null);
                    }
                    try
                    {
                        response.Content.Headers.TryAddWithoutValidation(headerName, headerValue);
                    }
                    catch
                    {
                        // Ignore headers that cannot be added
                    }
                }
                else
                {
                    try
                    {
                        response.Headers.TryAddWithoutValidation(headerName, headerValue);
                    }
                    catch
                    {
                        // Ignore headers that cannot be added
                    }
                }
            }

            // Set response content
            var responseStream = webResponse.GetResponseStream();
            if (responseStream != null)
            {
                if (completionOption == HttpCompletionOption.ResponseContentRead)
                {
                    // Read the entire response into memory
                    var memoryStream = new MemoryStream();
                    responseStream.CopyTo(memoryStream);
                    memoryStream.Position = 0;
                    response.Content = new StreamContent(memoryStream);
                    responseStream.Dispose();
                }
                else
                {
                    // Return the stream directly
                    response.Content = new StreamContent(responseStream);
                }

                // Copy content headers again if we just created the content
                foreach (var headerName in webResponse.Headers.AllKeys)
                {
                    var headerValue = webResponse.Headers[headerName];
                    if (headerValue == null) continue;

                    if (headerName.StartsWith("Content-", StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            response.Content.Headers.TryAddWithoutValidation(headerName, headerValue);
                        }
                        catch
                        {
                            // Ignore headers that cannot be added
                        }
                    }
                }
            }

            return response;
        }
#pragma warning restore SYSLIB0014

        /// <summary>
        /// Disposes the underlying <see cref="HttpClient"/>.
        /// </summary>
        public void Dispose()
        {
            if (this != Shared)
            {
                if (_disposeClient)
                {
                    Client.Dispose();
                }
            }

            GC.SuppressFinalize(this);
        }

        static HttpClient CreateDefaultClient(HttpTransportOptions? options = null)
        {
            return CreateCustomClient();
        }

        public static HttpClient CreateCustomClient(HttpTransportOptions? options = null)
        {
            var httpMessageHandler = CreateDefaultHandler(options);
            return new HttpClient(httpMessageHandler)
            {
                // Timeouts are handled by the pipeline
                Timeout = Timeout.InfiniteTimeSpan,
            };
        }

#if NETCOREAPP
        private static HttpMessageHandler CreateDefaultHandler(HttpTransportOptions? options = null)
        {
            var opt = options ?? new HttpTransportOptions();
            var handler = new SocketsHttpHandler {
                ConnectTimeout = opt.ConnectTimeout.GetValueOrDefault(HttpTransportOptions.DEFAULT_CONNECT_TIMEOUT),
                Expect100ContinueTimeout = opt.ExpectContinueTimeout.GetValueOrDefault(HttpTransportOptions.DEFAULT_EXPECT_CONTINUE_TIMEOUT),
                PooledConnectionIdleTimeout = opt.IdleConnectionTimeout.GetValueOrDefault(HttpTransportOptions.DEFAULT_IDLE_CONNECTION_TIMEOUT),
                KeepAlivePingTimeout = opt.KeepAliveTimeout.GetValueOrDefault(HttpTransportOptions.DEFAULT_KEEP_ALIVE_TIMEOUT),
                MaxConnectionsPerServer = opt.MaxConnections.GetValueOrDefault(HttpTransportOptions.DEFAULT_MAX_CONNECTIONS),
                AllowAutoRedirect = opt.EnabledRedirect.GetValueOrDefault(false),
                Proxy = opt.HttpProxy
            };
            if (opt.InsecureSkipVerify.GetValueOrDefault(false))
            {
                handler.SslOptions = new SslClientAuthenticationOptions
                {
                    RemoteCertificateValidationCallback = delegate { return true; },
                };
            }
            return handler;
        }
#else
        private static HttpMessageHandler CreateDefaultHandler(HttpTransportOptions? options = null)
        {
            var opt = options ?? new HttpTransportOptions();
            var handler = new HttpClientHandler
            {
                MaxConnectionsPerServer = opt.MaxConnections.GetValueOrDefault(HttpTransportOptions.DEFAULT_MAX_CONNECTIONS),
                AllowAutoRedirect = opt.EnabledRedirect.GetValueOrDefault(false),
                Proxy = opt.HttpProxy
            };
            if (opt.InsecureSkipVerify.GetValueOrDefault(false))
            {
                handler.ServerCertificateCustomValidationCallback = delegate { return true; };
            }
            return handler;
        }
#endif

    }
}
