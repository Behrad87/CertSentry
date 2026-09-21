using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using CertSentry.Enums;
using CertSentry.Models;

namespace CertSentry.Services;

public interface ICertificateStoreService
{
    Task<List<CertificateItem>> GetCertificatesAsync(StoreName storeName, StoreLocation storeLocation, bool devCertsOnly = false);
    Task<List<CertificateItem>> GetAllDevCertificatesAsync(bool includeLocalMachine = false);
    Task<CertificateItem?> FindByThumbprintAsync(string thumbprint, StoreLocation storeLocation = StoreLocation.CurrentUser);
    Task<bool> DeleteCertificateAsync(string thumbprint, StoreName storeName, StoreLocation storeLocation);
    Task<bool> TrustCertificateAsync(string thumbprint, StoreLocation sourceLocation = StoreLocation.CurrentUser);
    Task<bool> InstallCertificateAsync(X509Certificate2 certificate, StoreName storeName, StoreLocation storeLocation);
    Task<byte[]> ExportCertificateAsync(string thumbprint, StoreName storeName, StoreLocation storeLocation, bool asPfx, string? password = null);
    CertificateItem ConvertToModel(X509Certificate2 cert, string storeName, StoreLocation storeLocation);
    bool IsDevOrLocalhostCertificate(X509Certificate2 cert);
}

public class CertificateStoreService : ICertificateStoreService
{
    private const string AspNetHttpsOid = "1.3.6.1.4.1.311.84.1.1";

    public async Task<List<CertificateItem>> GetCertificatesAsync(StoreName storeName, StoreLocation storeLocation, bool devCertsOnly = false)
    {
        return await Task.Run(() =>
        {
            var results = new List<CertificateItem>();
            try
            {
                using var store = new X509Store(storeName, storeLocation);
                store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);

                foreach (var cert in store.Certificates)
                {
                    if (devCertsOnly && !IsDevOrLocalhostCertificate(cert))
                        continue;

                    results.Add(ConvertToModel(cert, storeName.ToString(), storeLocation));
                }
            }
            catch (Exception)
            {
                // Graceful fallback if a store requires elevation or fails to open
            }

            return results;
        });
    }

    public async Task<List<CertificateItem>> GetAllDevCertificatesAsync(bool includeLocalMachine = false)
    {
        var allDevCerts = new List<CertificateItem>();
        var userMy = await GetCertificatesAsync(StoreName.My, StoreLocation.CurrentUser, devCertsOnly: true);
        var userRoot = await GetCertificatesAsync(StoreName.Root, StoreLocation.CurrentUser, devCertsOnly: true);

        allDevCerts.AddRange(userMy);
        allDevCerts.AddRange(userRoot);

        if (includeLocalMachine)
        {
            var machineMy = await GetCertificatesAsync(StoreName.My, StoreLocation.LocalMachine, devCertsOnly: true);
            var machineRoot = await GetCertificatesAsync(StoreName.Root, StoreLocation.LocalMachine, devCertsOnly: true);
            allDevCerts.AddRange(machineMy);
            allDevCerts.AddRange(machineRoot);
        }

        return allDevCerts;
    }

    public async Task<CertificateItem?> FindByThumbprintAsync(string thumbprint, StoreLocation storeLocation = StoreLocation.CurrentUser)
    {
        return await Task.Run(() =>
        {
            var normalized = thumbprint.Replace(" ", "").Trim().ToUpperInvariant();
            var storeNames = new[] { StoreName.My, StoreName.Root, StoreName.CertificateAuthority };

            foreach (var sName in storeNames)
            {
                try
                {
                    using var store = new X509Store(sName, storeLocation);
                    store.Open(OpenFlags.ReadOnly);
                    var matches = store.Certificates.Find(X509FindType.FindByThumbprint, normalized, false);
                    if (matches.Count > 0)
                    {
                        return ConvertToModel(matches[0], sName.ToString(), storeLocation);
                    }
                }
                catch { }
            }
            return null;
        });
    }

    public async Task<bool> DeleteCertificateAsync(string thumbprint, StoreName storeName, StoreLocation storeLocation)
    {
        return await Task.Run(() =>
        {
            try
            {
                var normalized = thumbprint.Replace(" ", "").Trim().ToUpperInvariant();
                using var store = new X509Store(storeName, storeLocation);
                store.Open(OpenFlags.ReadWrite);
                var matches = store.Certificates.Find(X509FindType.FindByThumbprint, normalized, false);
                if (matches.Count == 0) return false;

                foreach (var match in matches)
                {
                    store.Remove(match);
                }
                return true;
            }
            catch
            {
                return false;
            }
        });
    }

    public async Task<bool> TrustCertificateAsync(string thumbprint, StoreLocation sourceLocation = StoreLocation.CurrentUser)
    {
        return await Task.Run(() =>
        {
            try
            {
                var normalized = thumbprint.Replace(" ", "").Trim().ToUpperInvariant();
                X509Certificate2? targetCert = null;

                using (var sourceStore = new X509Store(StoreName.My, sourceLocation))
                {
                    sourceStore.Open(OpenFlags.ReadOnly);
                    var matches = sourceStore.Certificates.Find(X509FindType.FindByThumbprint, normalized, false);
                    if (matches.Count > 0)
                    {
                        targetCert = new X509Certificate2(matches[0]);
                    }
                }

                if (targetCert == null) return false;

                using (var rootStore = new X509Store(StoreName.Root, StoreLocation.CurrentUser))
                {
                    rootStore.Open(OpenFlags.ReadWrite);
                    rootStore.Add(targetCert);
                }
                return true;
            }
            catch
            {
                return false;
            }
        });
    }

    public async Task<bool> InstallCertificateAsync(X509Certificate2 certificate, StoreName storeName, StoreLocation storeLocation)
    {
        return await Task.Run(() =>
        {
            try
            {
                using var store = new X509Store(storeName, storeLocation);
                store.Open(OpenFlags.ReadWrite);
                store.Add(certificate);
                return true;
            }
            catch
            {
                return false;
            }
        });
    }

    public async Task<byte[]> ExportCertificateAsync(string thumbprint, StoreName storeName, StoreLocation storeLocation, bool asPfx, string? password = null)
    {
        return await Task.Run(() =>
        {
            var normalized = thumbprint.Replace(" ", "").Trim().ToUpperInvariant();
            using var store = new X509Store(storeName, storeLocation);
            store.Open(OpenFlags.ReadOnly);
            var matches = store.Certificates.Find(X509FindType.FindByThumbprint, normalized, false);
            if (matches.Count == 0)
                throw new InvalidOperationException($"Certificate with thumbprint {thumbprint} not found.");

            var cert = matches[0];
            if (asPfx)
            {
                return cert.Export(X509ContentType.Pfx, password ?? string.Empty);
            }
            else
            {
                var pem = cert.ExportCertificatePem();
                return Encoding.UTF8.GetBytes(pem);
            }
        });
    }

    public bool IsDevOrLocalhostCertificate(X509Certificate2 cert)
    {
        var subject = cert.Subject.ToLowerInvariant();
        var issuer = cert.Issuer.ToLowerInvariant();
        var friendlyName = (cert.FriendlyName ?? string.Empty).ToLowerInvariant();

        if (subject.Contains("cn=localhost") ||
            subject.Contains("127.0.0.1") ||
            subject.Contains(".local") ||
            subject.Contains(".test") ||
            subject.Contains(".internal"))
            return true;

        if (issuer.Contains("aspnet") ||
            issuer.Contains("mkcert") ||
            issuer.Contains("certsentry") ||
            issuer.Contains("localhost"))
            return true;

        if (friendlyName.Contains("asp.net core https") ||
            friendlyName.Contains("iis express") ||
            friendlyName.Contains("certsentry") ||
            friendlyName.Contains("mkcert"))
            return true;

        // Check ASP.NET HTTPS OID
        foreach (var ext in cert.Extensions)
        {
            if (ext.Oid?.Value == AspNetHttpsOid)
                return true;

            if (ext is X509SubjectAlternativeNameExtension sanExt)
            {
                foreach (var dns in sanExt.EnumerateDnsNames())
                {
                    var d = dns.ToLowerInvariant();
                    if (d == "localhost" || d.EndsWith(".localhost") || d.EndsWith(".test") || d.EndsWith(".local") || d.Contains("docker"))
                        return true;
                }
                foreach (var ip in sanExt.EnumerateIPAddresses())
                {
                    if (IPAddress.IsLoopback(ip))
                        return true;
                }
            }
        }

        return false;
    }

    public CertificateItem ConvertToModel(X509Certificate2 cert, string storeName, StoreLocation storeLocation)
    {
        var item = new CertificateItem
        {
            Thumbprint = cert.Thumbprint,
            Subject = cert.Subject,
            Issuer = cert.Issuer,
            FriendlyName = cert.FriendlyName,
            SerialNumber = cert.SerialNumber,
            NotBefore = cert.NotBefore,
            NotAfter = cert.NotAfter,
            HasPrivateKey = cert.HasPrivateKey,
            StoreName = storeName,
            StoreLocation = storeLocation
        };

        // Common Name extraction
        var match = System.Text.RegularExpressions.Regex.Match(cert.Subject, @"CN=([^,]+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        item.CommonName = match.Success ? match.Groups[1].Value.Trim() : cert.Subject;

        // Subject Alternative Names
        foreach (var ext in cert.Extensions)
        {
            if (ext.Oid?.Value == AspNetHttpsOid)
            {
                item.IsAspNetCoreDevCert = true;
            }

            if (ext is X509SubjectAlternativeNameExtension sanExt)
            {
                foreach (var dns in sanExt.EnumerateDnsNames())
                    item.SubjectAlternativeNames.Add(dns);
                foreach (var ip in sanExt.EnumerateIPAddresses())
                    item.SubjectAlternativeNames.Add(ip.ToString());
            }

            if (ext is X509EnhancedKeyUsageExtension ekuExt)
            {
                foreach (var oid in ekuExt.EnhancedKeyUsages)
                {
                    var friendly = oid.FriendlyName ?? oid.Value ?? "Unknown";
                    item.EnhancedKeyUsages.Add(friendly);
                }
            }

            if (ext is X509BasicConstraintsExtension bcExt)
            {
                item.IsCertificateAuthority = bcExt.CertificateAuthority;
            }
        }

        // Key algorithm and size
        if (cert.GetRSAPublicKey() is { } rsa)
        {
            item.KeyAlgorithm = "RSA";
            item.KeySize = rsa.KeySize;
        }
        else if (cert.GetECDsaPublicKey() is { } ecdsa)
        {
            item.KeyAlgorithm = "ECDSA";
            item.KeySize = ecdsa.KeySize;
        }
        else
        {
            item.KeyAlgorithm = cert.PublicKey.Oid.FriendlyName ?? "Unknown";
            item.KeySize = cert.PublicKey.EncodedKeyValue.RawData.Length * 8;
        }

        item.SignatureAlgorithm = cert.SignatureAlgorithm.FriendlyName ?? cert.SignatureAlgorithm.Value ?? "Unknown";
        item.IsSelfSigned = cert.Subject.Equals(cert.Issuer, StringComparison.OrdinalIgnoreCase);
        item.IsLocalhostCert = IsDevOrLocalhostCertificate(cert);

        // Status calculation
        var now = DateTime.Now;
        if (cert.NotAfter < now)
        {
            item.HealthStatus = CertificateHealthStatus.Expired;
            item.StatusMessage = $"Expired on {cert.NotAfter:yyyy-MM-dd}";
        }
        else if (cert.NotBefore > now)
        {
            item.HealthStatus = CertificateHealthStatus.Warning;
            item.StatusMessage = $"Not valid before {cert.NotBefore:yyyy-MM-dd}";
        }
        else if (item.DaysUntilExpiration <= 30)
        {
            item.HealthStatus = CertificateHealthStatus.ExpiringSoon;
            item.StatusMessage = $"Expires in {item.DaysUntilExpiration} days";
        }
        else
        {
            item.HealthStatus = CertificateHealthStatus.Valid;
            item.StatusMessage = "Valid & Active";
        }

        // PEM string
        try
        {
            item.RawPem = cert.ExportCertificatePem();
        }
        catch
        {
            item.RawPem = Convert.ToBase64String(cert.RawData);
        }

        return item;
    }
}
