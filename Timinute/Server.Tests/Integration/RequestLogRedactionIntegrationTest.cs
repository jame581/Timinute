using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

namespace Timinute.Server.Tests.Integration
{
    // Proves the CRITICAL contract: Serilog request logging must NEVER write the query
    // string to the logs. Password-reset / email-confirmation links and the OIDC
    // login-callback carry secrets in the query string; leaking them to the console
    // (docker logs) or file sink is an account-takeover vector.
    //
    // Program.cs pins options.IncludeQueryInRequestPath = false. On Serilog.AspNetCore
    // 10.0.0 that is the single value governing whether RequestPath (used in both the
    // rendered message and the structured JSON) is IHttpRequestFeature.Path (no query)
    // or the raw target (path + query). See the report for why the EnrichDiagnosticContext
    // Set("RequestPath", ...) approach is a verified no-op on this version.
    //
    // The harness exercises the REAL configured pipeline through the file sink: config is
    // injected BEFORE the host builds (Program's UseSerilog reads context.Configuration)
    // to enable the rolling file sink to a unique temp dir, and a startup filter populates
    // IHttpRequestFeature.RawTarget exactly as Kestrel does (TestServer leaves it empty),
    // so the query really is available to leak if the guard regresses.
    [Collection("Integration")]
    public sealed class RequestLogRedactionIntegrationTest
    {
        private const string Secret = "SUPERSECRETTOKEN_DoNotLog";

        // Kestrel sets RawTarget to path + query; TestServer does not. Without this the
        // query simply cannot reach Serilog's GetPath, so the guard could never be
        // exercised and neither test would have teeth.
        private sealed class RawTargetStartupFilter : IStartupFilter
        {
            public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
            {
                return app =>
                {
                    app.Use(async (ctx, nxt) =>
                    {
                        var feature = ctx.Features.Get<IHttpRequestFeature>();
                        if (feature != null)
                            feature.RawTarget = ctx.Request.Path + ctx.Request.QueryString;
                        await nxt();
                    });
                    next(app);
                };
            }
        }

        // Control-only: a deliberately vulnerable second request-logging middleware that
        // DOES include the query (the exact misconfiguration Program.cs guards against).
        // It proves the file-sink harness genuinely captures a query secret when one is
        // logged — so the production test's ABSENCE of the secret is meaningful, not a
        // blind spot in the test setup.
        private sealed class LeakyRequestLoggingStartupFilter : IStartupFilter
        {
            public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
            {
                return app =>
                {
                    app.UseSerilogRequestLogging(o => o.IncludeQueryInRequestPath = true);
                    next(app);
                };
            }
        }

        private class FileSinkLogFactory : TiminuteApiFactory
        {
            public string LogDirectory { get; }
            private readonly string logPathTemplate;

            public FileSinkLogFactory()
            {
                LogDirectory = Path.Combine(Path.GetTempPath(), "timinute-logtest-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(LogDirectory);
                // Serilog's RollingInterval.Day rewrites this to timinute-<date>.log.
                logPathTemplate = Path.Combine(LogDirectory, "timinute-.log");
            }

            protected virtual bool Leaky => false;

            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                base.ConfigureWebHost(builder);

                builder.ConfigureTestServices(services =>
                {
                    services.AddTransient<IStartupFilter, RawTargetStartupFilter>();
                    if (Leaky)
                        services.AddTransient<IStartupFilter, LeakyRequestLoggingStartupFilter>();
                });

                // Injected BEFORE the host builds — Program.cs's UseSerilog reads these
                // keys off context.Configuration to wire up the opt-in rolling file sink.
                builder.ConfigureAppConfiguration(cfg =>
                {
                    cfg.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["Serilog:File:Enabled"] = "true",
                        ["Serilog:File:Path"] = logPathTemplate,
                        ["Serilog:MinimumLevel:Default"] = "Information",
                    });
                });
            }

            public string ReadAllLogText()
            {
                var sb = new System.Text.StringBuilder();
                foreach (var file in Directory.EnumerateFiles(LogDirectory, "*.log"))
                {
                    // The file sink may still hold its handle open; share it for reading.
                    using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    using var reader = new StreamReader(stream);
                    sb.Append(reader.ReadToEnd());
                }
                return sb.ToString();
            }

            public void Cleanup()
            {
                try
                {
                    if (Directory.Exists(LogDirectory))
                        Directory.Delete(LogDirectory, recursive: true);
                }
                catch (IOException)
                {
                    // Best-effort temp cleanup; ignore if a handle is still held.
                }
            }
        }

        private sealed class LeakyFileSinkLogFactory : FileSinkLogFactory
        {
            protected override bool Leaky => true;
        }

        private static async Task IssueRequestWithSecretAsync(FileSinkLogFactory factory)
        {
            using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost")
            });

            // A real controller route flows all the way through the request-logging
            // middleware and, unlike the SPA fallback, keeps its Request.Path. The extra
            // `code` query param is the planted secret.
            var from = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(-7).ToString("o"));
            var to = Uri.EscapeDataString(DateTimeOffset.UtcNow.ToString("o"));
            var response = await client.GetAsync(
                $"/Analytics/summary?From={from}&To={to}&TzOffsetMinutes=0&code={Secret}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task ProductionPipeline_Logs_Path_But_Never_The_Query_String_Secret()
        {
            var factory = new FileSinkLogFactory();
            try
            {
                await IssueRequestWithSecretAsync(factory);

                // Dispose the host so Serilog flushes and closes the file sink.
                factory.Dispose();

                var logText = factory.ReadAllLogText();

                Assert.False(string.IsNullOrEmpty(logText),
                    "Expected the file sink to have written at least one log event.");

                // The request WAS logged (proves the pipeline reached request logging)...
                Assert.Contains("responded", logText);
                // ...with the path portion preserved...
                Assert.Contains("/Analytics/summary", logText);
                // ...but the query string (and its secret) stripped, even though RawTarget
                // carried it. If IncludeQueryInRequestPath is flipped to true in Program.cs,
                // these assertions fail.
                Assert.DoesNotContain(Secret, logText);
                Assert.DoesNotContain("code=", logText);
            }
            finally
            {
                factory.Dispose();
                factory.Cleanup();
            }
        }

        // Teeth: proves the file-sink harness genuinely detects a query-string secret when
        // it is logged. A vulnerable request-logging middleware (IncludeQueryInRequestPath
        // = true — the misconfiguration the production guard prevents) leaks the secret to
        // the same sink. If this test did NOT see the secret, the production test above
        // would be meaningless.
        [Fact]
        public async Task LeakyPipeline_Control_Proves_Harness_Detects_The_Secret()
        {
            var factory = new LeakyFileSinkLogFactory();
            try
            {
                await IssueRequestWithSecretAsync(factory);

                factory.Dispose();

                var logText = factory.ReadAllLogText();

                Assert.False(string.IsNullOrEmpty(logText),
                    "Expected the file sink to have written at least one log event.");
                Assert.Contains(Secret, logText);
            }
            finally
            {
                factory.Dispose();
                factory.Cleanup();
            }
        }
    }
}
