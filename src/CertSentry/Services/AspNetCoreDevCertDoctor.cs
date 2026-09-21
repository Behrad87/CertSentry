using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using CertSentry.Models;

namespace CertSentry.Services;

public interface IAspNetCoreDevCertDoctor
{
    Task<AspNetDevCertDiagnosis> DiagnoseAsync();
    Task<string> RunCleanAsync();
    Task<string> RunTrustAsync();
    Task<string> RunCleanAndReissueAsync(Action<string>? progressCallback = null);
    Task<string> ExportPfxAsync(string targetFilePath, string password);
}

public class AspNetCoreDevCertDoctor : IAspNetCoreDevCertDoctor
{
    public async Task<AspNetDevCertDiagnosis> DiagnoseAsync()
    {
        var diagnosis = new AspNetDevCertDiagnosis();

        try
        {
            var output = await RunDotnetCliAsync("dev-certs https --check-trust-machine-readable");
            diagnosis.IsCliAvailable = true;

            var json = ExtractJson(output);
            if (!string.IsNullOrWhiteSpace(json))
            {
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };
                var list = JsonSerializer.Deserialize<List<AspNetDevCertJsonEntry>>(json, options) ?? new List<AspNetDevCertJsonEntry>();
                diagnosis.Certificates = list;
                diagnosis.HasCertificate = list.Count > 0;

                // Check duplicates (e.g. Version 5 and Version 6, or multiple certs)
                if (list.Count > 1)
                {
                    diagnosis.HasDuplicates = true;
                }

                if (list.Count > 0)
                {
                    diagnosis.ActiveVersion = list.Max(c => c.Version);
                    diagnosis.IsFullyTrusted = list.All(c => string.Equals(c.TrustLevel, "Full", StringComparison.OrdinalIgnoreCase));
                }

                // Craft summary message
                if (!diagnosis.HasCertificate)
                {
                    diagnosis.SummaryMessage = "No ASP.NET Core HTTPS developer certificate found.";
                    diagnosis.RecommendedAction = "Run 'dotnet dev-certs https --trust' or use CertSentry 1-Click Fix to create and trust one.";
                }
                else if (diagnosis.HasDuplicates)
                {
                    diagnosis.SummaryMessage = $"Found {list.Count} duplicate ASP.NET HTTPS certificates (Versions: {string.Join(", ", list.Select(c => $"v{c.Version}"))}).";
                    diagnosis.RecommendedAction = "Duplicate developer certificates can cause IIS Express, Kestrel, or browser SSL errors. Click 'Clean & Re-Issue' to resolve.";
                }
                else if (!diagnosis.IsFullyTrusted)
                {
                    diagnosis.SummaryMessage = "ASP.NET developer certificate is present but NOT trusted by the operating system.";
                    diagnosis.RecommendedAction = "Click 'Trust Dev Certificate' to install into Windows Trusted Root.";
                }
                else
                {
                    var active = list.OrderByDescending(c => c.Version).First();
                    diagnosis.SummaryMessage = $"ASP.NET HTTPS developer certificate (v{active.Version}) is installed and fully trusted.";
                    diagnosis.RecommendedAction = "No action required. ASP.NET Core HTTPS is healthy.";
                }
            }
            else
            {
                diagnosis.SummaryMessage = "CLI responded without certificate data.";
            }
        }
        catch (Exception ex)
        {
            diagnosis.IsCliAvailable = false;
            diagnosis.SummaryMessage = $"Failed to run 'dotnet dev-certs': {ex.Message}";
            diagnosis.RecommendedAction = "Ensure the .NET SDK is installed and available in PATH.";
        }

        return diagnosis;
    }

    public async Task<string> RunCleanAsync()
    {
        return await RunDotnetCliAsync("dev-certs https --clean");
    }

    public async Task<string> RunTrustAsync()
    {
        return await RunDotnetCliAsync("dev-certs https --trust");
    }

    public async Task<string> RunCleanAndReissueAsync(Action<string>? progressCallback = null)
    {
        progressCallback?.Invoke("Step 1/2: Cleaning all existing dev certificates ('dotnet dev-certs https --clean')...");
        var cleanOutput = await RunCleanAsync();
        progressCallback?.Invoke(cleanOutput);

        await Task.Delay(500);

        progressCallback?.Invoke("Step 2/2: Generating and trusting fresh developer certificate ('dotnet dev-certs https --trust')...");
        var trustOutput = await RunTrustAsync();
        progressCallback?.Invoke(trustOutput);

        progressCallback?.Invoke("1-Click Repair completed successfully!");
        return $"{cleanOutput}\n{trustOutput}";
    }

    public async Task<string> ExportPfxAsync(string targetFilePath, string password)
    {
        var dir = Path.GetDirectoryName(targetFilePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        var args = $"dev-certs https -ep \"{targetFilePath}\" -p \"{password}\"";
        return await RunDotnetCliAsync(args);
    }

    private static string ExtractJson(string output)
    {
        var start = output.IndexOf('[');
        var end = output.LastIndexOf(']');
        if (start >= 0 && end > start)
        {
            return output.Substring(start, end - start + 1);
        }
        return string.Empty;
    }

    private async Task<string> RunDotnetCliAsync(string arguments)
    {
        return await Task.Run(() =>
        {
            var psi = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = psi };
            process.Start();

            var stdout = process.StandardOutput.ReadToEnd();
            var stderr = process.StandardError.ReadToEnd();
            process.WaitForExit(15000);

            var combined = stdout;
            if (!string.IsNullOrWhiteSpace(stderr))
                combined += $"\nSTDERR: {stderr}";

            return combined.Trim();
        });
    }
}
