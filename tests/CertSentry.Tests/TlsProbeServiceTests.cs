using System.Collections.Generic;
using CertSentry.Services;
using FluentAssertions;
using Xunit;

namespace CertSentry.Tests;

public class TlsProbeServiceTests
{
    private readonly TlsProbeService _service = new(new CertificateStoreService());

    [Theory]
    [InlineData("localhost", new[] { "localhost", "127.0.0.1" }, true)]
    [InlineData("127.0.0.1", new[] { "localhost" }, true)]
    [InlineData("localhost", new[] { "127.0.0.1" }, true)]
    [InlineData("dev.local", new[] { "localhost", "dev.local" }, true)]
    [InlineData("api.dev.localhost", new[] { "*.dev.localhost" }, true)]
    [InlineData("web.test", new[] { "*.test" }, true)]
    [InlineData("notfound.local", new[] { "localhost", "127.0.0.1" }, false)]
    [InlineData("evil.com", new[] { "localhost" }, false)]
    public void IsHostMatchingSan_ShouldCorrectlyEvaluateHostnames(string host, string[] sans, bool expected)
    {
        // Act
        var result = _service.IsHostMatchingSan(host, sans);

        // Assert
        result.Should().Be(expected);
    }
}
