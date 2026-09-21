using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using CertSentry.Enums;
using CertSentry.Models;

namespace CertSentry.Services;

public interface ISystemDoctorService
{
    Task<SystemHealthReport> GenerateHealthReportAsync();
}

public class SystemDoctorService : ISystemDoctorService
{
    private readonly ICertificateStoreService _storeService;
    private readonly IAspNetCoreDevCertDoctor _aspNetDoctor;

    public SystemDoctorService(ICertificateStoreService storeService, IAspNetCoreDevCertDoctor aspNetDoctor)
    {
        _storeService = storeService;
        _aspNetDoctor = aspNetDoctor;
    }

    public async Task<SystemHealthReport> GenerateHealthReportAsync()
    {
        var report = new SystemHealthReport();
        int score = 100;

        // 1. Query Dev Certificates
        var devCerts = await _storeService.GetAllDevCertificatesAsync(includeLocalMachine: true);
        report.TotalDevCertsCount = devCerts.Count;
        report.KeyCertificates = devCerts.Take(6).ToList();

        // Expired count
        var expired = devCerts.Where(c => c.HealthStatus == CertificateHealthStatus.Expired).ToList();
        report.ExpiredCount = expired.Count;
        if (expired.Count > 0)
        {
            score -= Math.Min(30, expired.Count * 15);
            report.ActiveIssues.Add(new CertificateValidationIssue
            {
                Title = $"{expired.Count} Expired Certificate(s) Found",
                Description = $"Found expired localhost certificates: {string.Join(", ", expired.Select(c => c.CommonName))}.",
                Remedy = "Remove expired certificates using CertSentry Store Explorer.",
                Severity = ProbeSeverity.Error
            });
        }

        // Expiring soon count
        var expiring = devCerts.Where(c => c.HealthStatus == CertificateHealthStatus.ExpiringSoon).ToList();
        report.ExpiringSoonCount = expiring.Count;
        if (expiring.Count > 0)
        {
            score -= Math.Min(15, expiring.Count * 5);
            report.ActiveIssues.Add(new CertificateValidationIssue
            {
                Title = $"{expiring.Count} Certificate(s) Expiring Soon",
                Description = $"Certificates nearing expiration: {string.Join(", ", expiring.Select(c => $"{c.CommonName} ({c.DaysUntilExpiration}d)"))}.",
                Remedy = "Renew or regenerate these certificates before they lapse.",
                Severity = ProbeSeverity.Warning
            });
        }

        // Valid count
        report.ValidDevCertsCount = devCerts.Count(c => c.HealthStatus == CertificateHealthStatus.Valid);

        // 2. Query ASP.NET Dev-Certs Status
        try
        {
            var aspNetDiag = await _aspNetDoctor.DiagnoseAsync();
            report.AspNetCertInstalled = aspNetDiag.HasCertificate;
            report.AspNetCertTrusted = aspNetDiag.IsFullyTrusted;
            report.AspNetHasDuplicates = aspNetDiag.HasDuplicates;
            report.AspNetStatusText = aspNetDiag.SummaryMessage;

            if (!aspNetDiag.HasCertificate)
            {
                score -= 20;
                report.ActiveIssues.Add(new CertificateValidationIssue
                {
                    Title = "Missing ASP.NET Core Dev Certificate",
                    Description = "No ASP.NET Core HTTPS developer certificate is installed.",
                    Remedy = "Use 1-Click Fix to run 'dotnet dev-certs https --trust'.",
                    Severity = ProbeSeverity.Warning
                });
            }
            else if (aspNetDiag.HasDuplicates)
            {
                score -= 25;
                report.DuplicateCount = aspNetDiag.Certificates.Count;
                report.ActiveIssues.Add(new CertificateValidationIssue
                {
                    Title = "Duplicate ASP.NET HTTPS Certificates Detected",
                    Description = $"Detected multiple certificates ({string.Join(", ", aspNetDiag.Certificates.Select(c => $"v{c.Version}"))}).",
                    Remedy = "Click '1-Click Repair ASP.NET Dev-Certs' to purge duplicates and re-issue a clean certificate.",
                    Severity = ProbeSeverity.Error
                });
            }
            else if (!aspNetDiag.IsFullyTrusted)
            {
                score -= 25;
                report.ActiveIssues.Add(new CertificateValidationIssue
                {
                    Title = "Untrusted ASP.NET Developer Certificate",
                    Description = "The active ASP.NET HTTPS certificate is not trusted by the Windows Root CA store.",
                    Remedy = "Trust the certificate to eliminate browser security warnings.",
                    Severity = ProbeSeverity.Error
                });
            }
        }
        catch (Exception ex)
        {
            report.AspNetStatusText = $"Error diagnosing dev-certs: {ex.Message}";
        }

        // Clamp score between 0 and 100
        report.HealthScore = Math.Clamp(score, 0, 100);

        if (report.HealthScore >= 85)
        {
            report.HealthLevel = "Healthy";
            report.HealthSummary = "Localhost SSL/TLS configuration is in optimal health.";
        }
        else if (report.HealthScore >= 60)
        {
            report.HealthLevel = "Warning";
            report.HealthSummary = "Minor issues detected. Attention recommended.";
        }
        else
        {
            report.HealthLevel = "Critical";
            report.HealthSummary = "Critical certificate issues detected. Browser and tools may fail to connect.";
        }

        return report;
    }
}
