using Xunit;

namespace Timinute.Server.Tests.Integration
{
    // Serializes the WebApplicationFactory-based integration tests. Each factory's
    // UseSerilog sets the process-wide static Serilog.Log.Logger (and Serilog's request
    // logging writes to it), and the factories share the InMemory DB name — so running
    // these classes in parallel lets one host clobber another's logger/sink. That is
    // invisible to tests that never read logs, but RequestLogRedactionIntegrationTest
    // reads the file sink and needs a stable logger. Putting every factory-based class in
    // one collection makes xUnit run them one at a time (unit tests still run in parallel).
    [CollectionDefinition("Integration")]
    public sealed class IntegrationTestCollection { }
}
