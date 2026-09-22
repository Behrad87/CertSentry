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

    [Fact]
    public void TlsProbeViewModel_GenerateDiagnosticReport_ShouldContainExpectedSections()
    {
        var vm = new TlsProbeViewModel(null!, null!);
        vm.Result = new TlsProbeResult
        {
            TargetUrl = "https://localhost:5001",
            ResolvedIp = "127.0.0.1",
            Port = 5001,
            IsConnected = true,
            VerdictTitle = "Secure & Trusted",
            VerdictDescription = "Connection negotiated successfully.",
            DnsResolutionMs = 2,
            TcpConnectMs = 5,
            TlsHandshakeMs = 15,
            TlsProtocolVersion = "Tls13",
            NegotiatedCipherSuite = "TLS_AES_256_GCM_SHA384",
            ServerCertificate = new CertificateItem
            {
                CommonName = "localhost",
                Subject = "CN=localhost",
                Issuer = "CN=localhost",
                NotBefore = DateTime.UtcNow.AddDays(-10),
                NotAfter = DateTime.UtcNow.AddDays(355),
                Thumbprint = "ABCDEF1234567890",
                KeyAlgorithm = "RSA",
                KeySize = 2048,
                SignatureAlgorithm = "sha256RSA",
                HasPrivateKey = true
            },
            Issues = new()
            {
                new CertificateValidationIssue
                {
                    Title = "Test Issue",
                    Description = "Test issue description",
                    Remedy = "Test issue remedy",
                    Severity = ProbeSeverity.Warning
                }
            }
        };

        var report = vm.GenerateDiagnosticReport();
        report.Should().Contain("CertSentry TLS Endpoint Diagnostic Report");
        report.Should().Contain("https://localhost:5001");
        report.Should().Contain("TLS_AES_256_GCM_SHA384");
        report.Should().Contain("CN=localhost");
        report.Should().Contain("Test Issue");
        report.Should().Contain("Test issue remedy");
    }

    [Fact]
    public async Task PortScannerViewModel_ProbePortInDoctorAsync_ShouldSetTargetOnTlsProbeViewModel()
    {
        var fakeService = new FakePortScannerService();
        var tlsProbeVm = new TlsProbeViewModel(null!, null!);
        var portScannerVm = new PortScannerViewModel(fakeService, null, tlsProbeVm);

        var scanItem = new PortScanResult
        {
            Port = 8443,
            Status = PortServiceType.Https
        };

        await portScannerVm.ProbePortInDoctorAsync(scanItem);

        tlsProbeVm.TargetUrl.Should().Be("https://localhost:8443");
    }

    [Fact]
    public void AspNetCoreDoctorViewModel_ClearTerminal_ShouldResetOutput()
    {
        var vm = new AspNetCoreDoctorViewModel(null!);
        vm.TerminalOutput = "Some test logs";
        vm.ClearTerminal();
        vm.TerminalOutput.Should().BeEmpty();
    }
}
