namespace CertSentry.Enums;

/// <summary>
/// Represents the overall health and validation status of a certificate.
/// </summary>
public enum CertificateHealthStatus
{
    Valid,
    ExpiringSoon,
    Expired,
    Untrusted,
    MissingPrivateKey,
    Warning,
    Unknown
}

/// <summary>
/// Target Windows Certificate Store location.
/// </summary>
public enum StoreLocationType
{
    CurrentUser,
    LocalMachine
}

/// <summary>
/// Supported asymmetric key algorithms for certificate generation.
/// </summary>
public enum KeyAlgorithmType
{
    Rsa2048,
    Rsa3072,
    Rsa4096,
    EcdsaP256,
    EcdsaP384,
    EcdsaP521
}

/// <summary>
/// Severity level of TLS probe diagnostics and issues.
/// </summary>
public enum ProbeSeverity
{
    Success,
    Information,
    Warning,
    Error
}

/// <summary>
/// Status of a scanned network port.
/// </summary>
public enum PortServiceType
{
    Closed,
    Http,
    Https,
    Unknown
}
