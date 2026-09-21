using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CertSentry.Enums;
using CertSentry.Models;
using CertSentry.Services;
using CertSentry.ViewModels;
using FluentAssertions;
using Xunit;

namespace CertSentry.Tests;

public class ModelAndViewModelTests
{
    private class FakePortScannerService : IPortScannerService
    {
        public IReadOnlyList<int> DefaultDevPorts => new List<int> { 5001, 3000 };

        public Task<List<PortScanResult>> ScanPortsAsync(IEnumerable<int> ports, IProgress<PortScanResult>? progress = null, CancellationToken cancellationToken = default)
        {
            var results = new List<PortScanResult>();
            foreach (var port in ports)
            {
                var r = new PortScanResult
                {
                    Port = port,
                    Status = port == 5001 ? PortServiceType.Https : PortServiceType.Http,
                    CommonServiceName = "Test Service",
                    LatencyMs = 12
                };
                progress?.Report(r);
                results.Add(r);
            }
            return Task.FromResult(results);
        }

        public Task<PortScanResult> ScanSinglePortAsync(int port, int timeoutMs = 1200, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new PortScanResult { Port = port, Status = PortServiceType.Https });
        }
    }

    [Fact]
    public void CertificateItem_ExpirationSummary_ShouldFormatAccurately()
    {
        var expiredItem = new CertificateItem
        {
            NotAfter = DateTime.Now.AddDays(-10)
        };
        expiredItem.ExpirationSummary.Should().Contain("Expired");

        var futureItem = new CertificateItem
        {
            NotAfter = DateTime.Now.AddDays(45)
        };
        futureItem.ExpirationSummary.Should().Contain("Expires in 45 days");

        var tomorrowItem = new CertificateItem
        {
            NotAfter = DateTime.Now.AddDays(1)
        };
        tomorrowItem.ExpirationSummary.Should().Contain("Expires tomorrow");
    }

    [Fact]
    public void CertificateItem_SansDisplaySummary_ShouldFormatCorrectly()
    {
        var emptySans = new CertificateItem();
        emptySans.SansDisplaySummary.Should().Be("(No SANs)");

        var withSans = new CertificateItem
        {
            SubjectAlternativeNames = new() { "localhost", "127.0.0.1", "*.local" }
        };
        withSans.SansDisplaySummary.Should().Be("localhost, 127.0.0.1, *.local");
    }

    [Fact]
    public async Task PortScannerViewModel_ScanCustomPorts_ShouldScanGivenRanges()
    {
        // Arrange
        var fakeService = new FakePortScannerService();
        var vm = new PortScannerViewModel(fakeService);
        vm.CustomPortInput = "5000-5003, 8080";

        // Act
        await vm.ScanCustomPortsAsync();

        // Assert
        vm.Results.Should().HaveCount(5); // 5000, 5001, 5002, 5003, 8080
        vm.IsScanning.Should().BeFalse();
        vm.StatusMessage.Should().Contain("Scan completed");
    }

    [Fact]
    public async Task PortScannerViewModel_ScanCustomPorts_WithInvalidInput_SetsNotificationMessage()
    {
        // Arrange
        var fakeService = new FakePortScannerService();
        var vm = new PortScannerViewModel(fakeService);
        vm.CustomPortInput = "invalid, not-a-port";

        // Act
        await vm.ScanCustomPortsAsync();

        // Assert
        vm.Results.Should().BeEmpty();
        vm.NotificationMessage.Should().Contain("Please enter valid port numbers");
    }
}
