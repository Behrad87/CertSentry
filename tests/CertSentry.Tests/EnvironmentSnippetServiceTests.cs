using CertSentry.Services;
using FluentAssertions;
using Xunit;

namespace CertSentry.Tests;

public class EnvironmentSnippetServiceTests
{
    private readonly EnvironmentSnippetService _service = new();

    [Fact]
    public void GenerateSnippets_ShouldProduceValidConfigsForMajorRuntimes()
    {
        // Arrange
        var caPath = @"C:\certs\ca.crt";
        var keyPath = @"C:\certs\ca.key";
        var port = 5001;

        // Act
        var snippets = _service.GenerateSnippets(caPath, keyPath, port);

        // Assert
        snippets.Should().NotBeEmpty();
        snippets.Should().Contain(s => s.Category == "Node.js" && s.Code.Contains("NODE_EXTRA_CA_CERTS"));
        snippets.Should().Contain(s => s.Category == "Docker" && s.Code.Contains("update-ca-certificates"));
        snippets.Should().Contain(s => s.Category == "Git" && s.Code.Contains("http.sslCAInfo"));
        snippets.Should().Contain(s => s.Category == "cURL" && s.Code.Contains("--cacert"));
        snippets.Should().Contain(s => s.Category == "Python" && s.Code.Contains("REQUESTS_CA_BUNDLE"));
        snippets.Should().Contain(s => s.Category == "Vite" && s.Code.Contains("https:"));
    }
}
