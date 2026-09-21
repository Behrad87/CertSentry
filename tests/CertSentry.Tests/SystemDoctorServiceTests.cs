using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using CertSentry.Enums;
using CertSentry.Models;
using CertSentry.Services;
using FluentAssertions;
using Xunit;

namespace CertSentry.Tests;

public class SystemDoctorServiceTests
{
    private class FakeCertificateStoreService : ICertificateStoreService
    {
        public List<CertificateItem> DevCerts { get; set; } = new();

        public Task<List<CertificateItem>> GetAllDevCertificatesAsync(bool includeLocalMachine = true)
        {
            return Task.FromResult(DevCerts);
        }

        public Task<List<CertificateItem>> GetCertificatesAsync(StoreName storeName, StoreLocation storeLocation, bool devCertsOnly = false)
        {
            return Task.FromResult(new List<CertificateItem>());
        }

        public Task<CertificateItem?> FindByThumbprintAsync(string thumbprint, StoreLocation storeLocation = StoreLocation.CurrentUser) => Task.FromResult<CertificateItem?>(null);
        public Task<bool> DeleteCertificateAsync(string thumbprint, StoreName storeName, StoreLocation storeLocation) => Task.FromResult(true);
        public Task<bool> TrustCertificateAsync(string thumbprint, StoreLocation sourceLocation = StoreLocation.CurrentUser) => Task.FromResult(true);
        public Task<bool> InstallCertificateAsync(X509Certificate2 certificate, StoreName storeName, StoreLocation storeLocation) => Task.FromResult(true);
        public Task<byte[]> ExportCertificateAsync(string thumbprint, StoreName storeName, StoreLocation storeLocation, bool asPfx, string? password = null) => Task.FromResult(Array.Empty<byte>());
        public CertificateItem ConvertToModel(X509Certificate2 cert, string storeName, StoreLocation storeLocation) => new();
        public bool IsDevOrLocalhostCertificate(X509Certificate2 cert) => true;
    }

    private class FakeAspNetCoreDevCertDoctor : IAspNetCoreDevCertDoctor
    {
        public AspNetDevCertDiagnosis Diagnosis { get; set; } = new();

        public Task<AspNetDevCertDiagnosis> DiagnoseAsync() => Task.FromResult(Diagnosis);
        public Task<string> RunCleanAndReissueAsync(Action<string>? progress = null) => Task.FromResult("Success");
        public Task<string> RunTrustAsync() => Task.FromResult("Success");
        public Task<string> RunCleanAsync() => Task.FromResult("Success");
        public Task<string> ExportPfxAsync(string exportPath, string password) => Task.FromResult("Success");
    }

    [Fact]
    public async Task GenerateHealthReport_WhenEverythingHealthy_ShouldReturn100ScoreAndHealthyLevel()
    {
        // Arrange
        var fakeStore = new FakeCertificateStoreService
        {
            DevCerts = new List<CertificateItem>
            {
                new()
                {
                    CommonName = "localhost",
                    HealthStatus = CertificateHealthStatus.Valid,
                    NotAfter = DateTime.Now.AddDays(180)
                }
            }
        };

        var fakeDoctor = new FakeAspNetCoreDevCertDoctor
        {
            Diagnosis = new AspNetDevCertDiagnosis
            {
                HasCertificate = true,
                IsFullyTrusted = true,
                HasDuplicates = false,
                SummaryMessage = "ASP.NET Core HTTPS development certificate is valid and trusted."
            }
        };

        var service = new SystemDoctorService(fakeStore, fakeDoctor);

        // Act
        var report = await service.GenerateHealthReportAsync();

        // Assert
        report.Should().NotBeNull();
        report.HealthScore.Should().Be(100);
        report.HealthLevel.Should().Be("Healthy");
        report.ValidDevCertsCount.Should().Be(1);
        report.ExpiredCount.Should().Be(0);
        report.ExpiringSoonCount.Should().Be(0);
        report.ActiveIssues.Should().BeEmpty();
    }

    [Fact]
    public async Task GenerateHealthReport_WithExpiredAndExpiringCerts_ShouldPenalizeScore()
    {
        // Arrange
        var fakeStore = new FakeCertificateStoreService
        {
            DevCerts = new List<CertificateItem>
            {
                new()
                {
                    CommonName = "expired.local",
                    HealthStatus = CertificateHealthStatus.Expired,
                    NotAfter = DateTime.Now.AddDays(-5)
                },
                new()
                {
                    CommonName = "expiring.local",
                    HealthStatus = CertificateHealthStatus.ExpiringSoon,
                    NotAfter = DateTime.Now.AddDays(10)
                }
            }
        };

        var fakeDoctor = new FakeAspNetCoreDevCertDoctor
        {
            Diagnosis = new AspNetDevCertDiagnosis
            {
                HasCertificate = true,
                IsFullyTrusted = true,
                HasDuplicates = false,
                SummaryMessage = "Valid"
            }
        };

        var service = new SystemDoctorService(fakeStore, fakeDoctor);

        // Act
        var report = await service.GenerateHealthReportAsync();

        // Assert
        report.ExpiredCount.Should().Be(1);
        report.ExpiringSoonCount.Should().Be(1);
        report.HealthScore.Should().Be(80); // 100 - 15 (expired) - 5 (expiring) = 80
        report.HealthLevel.Should().Be("Warning");
        report.ActiveIssues.Should().HaveCount(2);
        report.ActiveIssues.Should().Contain(i => i.Severity == ProbeSeverity.Error);
        report.ActiveIssues.Should().Contain(i => i.Severity == ProbeSeverity.Warning);
    }

    [Fact]
    public async Task GenerateHealthReport_WithDuplicatesAndUntrusted_ShouldReportCriticalIssues()
    {
        // Arrange
        var fakeStore = new FakeCertificateStoreService();
        var fakeDoctor = new FakeAspNetCoreDevCertDoctor
        {
            Diagnosis = new AspNetDevCertDiagnosis
            {
                HasCertificate = true,
                IsFullyTrusted = false,
                HasDuplicates = true,
                Certificates = new()
                {
                    new() { Version = 5, Thumbprint = "AAAA" },
                    new() { Version = 6, Thumbprint = "BBBB" }
                },
                SummaryMessage = "Multiple certificates found."
            }
        };

        var service = new SystemDoctorService(fakeStore, fakeDoctor);

        // Act
        var report = await service.GenerateHealthReportAsync();

        // Assert
        report.AspNetHasDuplicates.Should().BeTrue();
        report.DuplicateCount.Should().Be(2);
        report.HealthScore.Should().Be(75); // 100 - 25
        report.ActiveIssues.Should().Contain(i => i.Title.Contains("Duplicate ASP.NET HTTPS Certificates"));
    }

    [Fact]
    public async Task GenerateHealthReport_WhenNoCertificateInstalled_ShouldFlagMissing()
    {
        // Arrange
        var fakeStore = new FakeCertificateStoreService();
        var fakeDoctor = new FakeAspNetCoreDevCertDoctor
        {
            Diagnosis = new AspNetDevCertDiagnosis
            {
                HasCertificate = false,
                IsFullyTrusted = false,
                HasDuplicates = false,
                SummaryMessage = "No certificate found."
            }
        };

        var service = new SystemDoctorService(fakeStore, fakeDoctor);

        // Act
        var report = await service.GenerateHealthReportAsync();

        // Assert
        report.AspNetCertInstalled.Should().BeFalse();
        report.HealthScore.Should().Be(80); // 100 - 20
        report.ActiveIssues.Should().Contain(i => i.Title.Contains("Missing ASP.NET Core Dev Certificate"));
    }
}
