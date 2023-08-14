using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace HyperSharp.Protocol
{
    /// <summary>
    /// Represents the context of an HTTP request.
    /// </summary>
    [DebuggerDisplay("{ToString(),nq}")]
    public class HyperContext
    {
        /// <summary>
        /// The currently supported HTTP versions.
        /// </summary>
        private static readonly FrozenDictionary<Version, byte[]> _httpVersions = new Dictionary<Version, byte[]>()
        {
            [HttpVersion.Version10] = "HTTP/1.0 "u8.ToArray(),
            [HttpVersion.Version11] = "HTTP/1.1 "u8.ToArray(),
        }.ToFrozenDictionary();

        /// <summary>
        /// The HTTP method of the request.
        /// </summary>
        public HttpMethod Method { get; init; }

        /// <summary>
        /// The requested URI of the HTTP request.
        /// </summary>
        public Uri Route { get; init; }

        /// <summary>
        /// The HTTP version of the request.
        /// </summary>
        public Version Version { get; init; }

        /// <summary>
        /// The client headers of the request.
        /// </summary>
        public HyperHeaderCollection Headers { get; init; }

        /// <summary>
        /// The currently opened connection to the client.
        /// </summary>
        public HyperConnection Connection { get; init; }

        /// <summary>
        /// Any metadata associated with the request, explicitly set by the registered responders.
        /// </summary>
        public Dictionary<string, string> Metadata { get; init; } = new();

        /// <summary>
        /// Whether or not the request has been responded to.
        /// </summary>
        public bool HasResponded { get; private set; }

        /// <summary>
        /// The body of the request.
        /// </summary>
        public PipeReader BodyReader => Connection.StreamReader;

        /// <summary>
        /// Creates a new <see cref="HyperContext"/> with the specified parameters.
        /// </summary>
        /// <param name="method">The HTTP method of the request.</param>
        /// <param name="route">The requested URI of the HTTP request.</param>
        /// <param name="version">The HTTP version of the request.</param>
        /// <param name="headers">The client headers of the request.</param>
        /// <param name="connection">The currently opened connection to the client.</param>
        public HyperContext(HttpMethod method, Uri route, Version version, HyperHeaderCollection headers, HyperConnection connection)
        {
            ArgumentNullException.ThrowIfNull(method);
            ArgumentNullException.ThrowIfNull(route);
            ArgumentNullException.ThrowIfNull(version);
            ArgumentNullException.ThrowIfNull(headers);
            ArgumentNullException.ThrowIfNull(connection);

            Version = version;
            Method = method;
            Headers = headers;
            Connection = connection;
            Route = headers.TryGetValue("Host", out IReadOnlyList<string>? host)
                ? new Uri($"http://{host[0]}{route.OriginalString}")
                : new Uri(connection.Server.Configuration._host, route);
        }

        /// <summary>
        /// Responds to the request with the specified status, and serializes the body using the specified <see cref="JsonSerializerOptions"/>.
        /// </summary>
        /// <param name="status">The status to respond with.</param>
        /// <param name="serializerOptions">The <see cref="JsonSerializerOptions"/> to use when serializing the body.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> to use when writing the response.</param>
        public virtual async Task RespondAsync(HyperStatus status, JsonSerializerOptions? serializerOptions = null, CancellationToken cancellationToken = default)
        {
            // Grab the base network stream to write our ASCII headers to.
            // TODO: Find a solution which allows modification of the body (Gzip) and the base stream (SSL).

            // Write request line
            await Connection.StreamWriter.WriteAsync(_httpVersions[Version], cancellationToken);
            await Connection.StreamWriter.WriteAsync(Encoding.ASCII.GetBytes($"{(int)status.Code} {status.Code}"), cancellationToken);
            await Connection.StreamWriter.WriteAsync("\r\n"u8.ToArray(), cancellationToken);

            // Serialize body ahead of time due to headers
            byte[] content = JsonSerializer.SerializeToUtf8Bytes(status.Body, serializerOptions ?? Connection.Server.Configuration.JsonSerializerOptions);

            // Write headers
            status.Headers.TryAdd("Date", DateTime.UtcNow.ToString("R"));
            status.Headers.TryAdd("Content-Length", content.Length.ToString());
            status.Headers.TryAdd("Content-Type", "application/json; charset=utf-8");
            status.Headers.TryAdd("Server", Connection.Server.Configuration.ServerName);

            foreach (string headerName in status.Headers.Keys)
            {
                await Connection.StreamWriter.WriteAsync(Encoding.ASCII.GetBytes(headerName), cancellationToken);
                await Connection.StreamWriter.WriteAsync(": "u8.ToArray(), cancellationToken);

                if (!status.Headers.TryGetValue(headerName, out IReadOnlyList<byte[]>? headerValues))
                {
                    // This shouldn't be able to happen, but just in case.
                    await Connection.StreamWriter.WriteAsync("\r\n"u8.ToArray(), cancellationToken);
                    continue;
                }

                if (headerValues.Count == 1)
                {
                    await Connection.StreamWriter.WriteAsync(headerValues[0], cancellationToken);
                }
                else
                {
                    foreach (byte[] value in headerValues)
                    {
                        await Connection.StreamWriter.WriteAsync(value, cancellationToken);
                        await Connection.StreamWriter.WriteAsync(", "u8.ToArray(), cancellationToken);
                    }
                }

                await Connection.StreamWriter.WriteAsync("\r\n"u8.ToArray(), cancellationToken);
            }
            await Connection.StreamWriter.WriteAsync("\r\n"u8.ToArray(), cancellationToken);

            // Write body
            if (content.Length != 0)
            {
                await Connection.StreamWriter.WriteAsync(content, cancellationToken);
            }

            await BodyReader.CompleteAsync();
            await Connection.StreamWriter.CompleteAsync();
            HasResponded = true;
        }

        /// <inheritdoc/>
        public override string ToString() => $"{Method} {Route} HTTP {Version}, {Headers.Count:N0} header{(Headers.Count == 1 ? "" : "s")}, {Metadata.Count:N0} metadata item{(Metadata.Count == 1 ? "" : "s")}";
    }
}
