# CertSentry 🛡️
> **Localhost SSL/TLS Doctor** — The modern, open-source desktop utility for diagnosing, repairing, inspecting, and managing localhost HTTPS development certificates.

Built with **C# (.NET 10 / WPF)**, **Lepo WPF-UI 4.3.0 (Fluent Dark Theme with Mica)**, and **CommunityToolkit.Mvvm 8.4.2**.

---

## 🌟 The Problem CertSentry Solves

Localhost SSL/TLS debugging is notoriously frustrating for web and cloud developers:
- **`NET::ERR_CERT_AUTHORITY_INVALID` & `UNABLE_TO_VERIFY_LEAF_SIGNATURE`**: Browsers, Node.js, Python, and cURL refusing to trust your self-signed dev certificates.
- **Broken ASP.NET Core Dev-Certs**: Conflicting duplicate versions (`v5`, `v6`, etc.) lingering in Windows certificate stores, causing Kestrel and IIS Express binding failures.
- **Missing Subject Alternative Names (SAN)**: Modern browsers rejecting certificates because `localhost` or `127.0.0.1` is missing from the SAN list.
- **Cryptic Certificate Stores**: Wrestling with `certmgr.msc` and `certlm.msc` just to locate, export, or delete a localhost certificate.
- **Container & Toolchain Boundaries**: Needing to extract trusted Root CAs for Docker containers, WSL, Git for Windows, and Vite dev servers.

**CertSentry** gives developers instant visibility, live TLS probing, 1-click repairs, and a complete certificate generation studio inside a Windows 11 Fluent dark UI.

---

## ✨ Key Features

### 🩺 1. Health Doctor Dashboard
- **Automated Health Rating (0–100 Score)**: Instant assessment of your system's localhost certificates and configuration.
- **Summary Metrics**:
  - ASP.NET Core HTTPS dev-cert status (version, trust level, duplicate warnings).
  - Dev certificate counts across Windows stores (`Valid`, `Expiring Soon`, `Expired`).
  - Active localhost HTTPS listening ports.
- **1-Click Quick Actions**:
  - *Repair ASP.NET Dev-Certs*: Purges stale/duplicate versions and re-issues a clean, trusted certificate.
  - *Purge Stale Localhost Certs*: Cleans expired or orphaned dev certificates.

### 🔬 2. TLS Connection Doctor & Live Handshake Probe
- Probe any local URL or host:port (e.g. `https://localhost:5001`, `https://127.0.0.1:3000`, `https://localhost:5173`, `https://localhost:8443`).
- **Socket Latency Metrics**: DNS resolution, TCP connection, and TLS handshake timing.
- **Cryptographic Details**:
  - Negotiated protocol (TLS 1.3 / TLS 1.2).
  - Negotiated Cipher Suite (e.g., `TLS_AES_256_GCM_SHA384`, `TLS_ECDHE_RSA_WITH_AES_256_GCM_SHA384`).
  - Application Protocol (ALPN): HTTP/2 (`h2`), HTTP/1.1 (`http/1.1`).
  - Public Key type, size, and hash algorithms.
- **Interactive Certificate Chain Inspector**: Leaf -> Intermediate -> Root CA hierarchy with thumbprints and validity status.
- **Subject Alternative Name (SAN) Matcher**: RFC 6125 wildcard and loopback alias validation (`localhost`, `127.0.0.1`, `::1`).
- **Plain English Diagnostic Verdict**: Detailed explanation of `SslPolicyErrors` with actionable remediation steps.
- **1-Click Actions**: Trust the remote certificate into Windows Root, copy public PEM, or copy the full diagnostic report.

### 🗄️ 3. Certificate Store Explorer
- Dual-store browser (`CurrentUser` and `LocalMachine`) across:
  - `Personal` (`My`)
  - `Trusted Root Certification Authorities` (`Root`)
  - `Intermediate Certification Authorities` (`CertificateAuthority`)
- **Intelligent Dev Filter**: 1-click toggle to isolate localhost and dev certificates (`localhost`, `127.0.0.1`, `dev.local`, `*.test`, `ASP.NET`, `mkcert`).
- **Search**: Instant filtering across Subject, Issuer, Thumbprint, and SANs.
- **Certificate Inspector Panel**:
  - Common Name, Subject, Issuer, Serial Number, Thumbprint.
  - Expiry countdown badge (Valid, Expiring Soon, Expired).
  - Enhanced Key Usages (Server Auth, Client Auth).
  - Raw PEM viewer with 1-click copy.
- **Export & Management**:
  - Export as `.crt` / `.pem` (Base64 PEM).
  - Export as `.pfx` (PKCS#12 bundle with private key).
  - Trust into `CurrentUser\Root`.
  - Delete certificate with confirmation dialog.

### 🎨 4. Certificate Studio & Generator
- Issue custom self-signed certificates or create a custom Local Development Root CA.
- **Configurable Parameters**:
  - Common Name (CN): e.g. `localhost`, `myapp.local`, `*.test`.
  - Multi-SAN editor: Pre-populated with `localhost`, `127.0.0.1`, `::1`, `host.docker.internal`, `*.dev.localhost`.
  - Key Algorithm: RSA (2048, 4096-bit) or ECDSA (NIST P-256).
  - Validity: 30 days to 5 years.
  - Server & Client Authentication flags.
- **Output & Automation**:
  - 1-Click install to Windows Stores (`Personal` and `Trusted Root`).
  - Export PFX bundle.
  - Export separate `.crt` and `.key` files.

### ⚡ 5. ASP.NET Core Dev-Cert Specialist
- Deep integration with `dotnet dev-certs https`.
- Automatic detection of duplicate version conflicts (`v5`, `v6`, etc.).
- 1-Click Clean & Re-Issue execution with live output terminal.
- Export dev certificate with password for Docker Compose.

### 📡 6. Localhost HTTPS Port Scanner
- Rapid asynchronous scan of common dev ports:
  - `3000` (React / Next.js), `3001`, `4200` (Angular), `443` (HTTPS), `5000/5001` (ASP.NET Core), `5173` (Vite), `7000/7001` (Minimal APIs), `8000/8080` (Django / Tomcat / Vue), `8443` (Dev proxy / Caddy), `9000/9443` (Docker / Portainer).
  - Custom port ranges support (e.g. `8000-8020`, `3000, 5001`).
- Identifies active HTTPS, HTTP, and closed ports with server certificate details.

### 📋 7. Runtime & Dev Integration Snippets
- Instant copy-paste snippets to configure runtimes to trust your local certificates:
  - **Node.js**: `$env:NODE_EXTRA_CA_CERTS="C:\certs\ca.crt"` / `export NODE_EXTRA_CA_CERTS=...`
  - **Git for Windows**: `git config --global http.sslCAInfo "C:\certs\ca.crt"`
  - **cURL**: `curl --cacert "C:\certs\ca.crt" https://localhost:5001/`
  - **Python**: `export REQUESTS_CA_BUNDLE="C:\certs\ca.crt"`
  - **Docker**: Dockerfile CA installation instructions (`update-ca-certificates`).
  - **WSL (Ubuntu / Debian)**: Sync and trust commands.
  - **Vite**: `vite.config.ts` HTTPS server configuration template.

### ⚙️ 8. Fluent UI & Personalization
- Windows 11 Fluent Design with Mica backdrop.
- Theme selector: Dark (Default Fluent Dark), Light, System Default.
- System diagnostics & runtime environment inspection.

---

## 🏗️ Architecture & Technology Stack

```
CertSentry/
├── CertSentry.slnx
├── src/
│   └── CertSentry/
│       ├── App.xaml / App.xaml.cs                # Host builder, DI, Fluent theme setup
│       ├── Enums/
│       │   └── CoreEnums.cs                      # HealthStatus, KeyAlgorithms, Severities
│       ├── Models/
│       │   ├── CertificateItem.cs                # Store certificate model
│       │   ├── TlsProbeResult.cs                 # Socket & handshake probe model
│       │   └── HealthAndDiagnosisModels.cs       # Dev-certs, port scan, and doctor models
│       ├── Services/
│       │   ├── CertificateStoreService.cs        # Windows Store query & manipulation
│       │   ├── TlsProbeService.cs                # Live SslStream TLS handshake & analyzer
│       │   ├── CertificateGeneratorService.cs    # X509 CertificateRequest generator
│       │   ├── AspNetCoreDevCertDoctor.cs        # dotnet dev-certs CLI wrapper
│       │   ├── PortScannerService.cs             # Asynchronous port & TLS probe
│       │   ├── EnvironmentSnippetService.cs      # Toolchain configuration generator
│       │   ├── SystemDoctorService.cs            # Health score aggregator
│       │   └── PageService.cs                    # WPF-UI navigation provider
│       ├── ViewModels/
│       │   ├── MainWindowViewModel.cs
│       │   ├── DashboardViewModel.cs
│       │   ├── TlsProbeViewModel.cs
│       │   ├── StoreExplorerViewModel.cs
│       │   ├── CertGeneratorViewModel.cs
│       │   ├── AspNetCoreDoctorViewModel.cs
│       │   ├── PortScannerViewModel.cs
│       │   ├── SnippetsViewModel.cs
│       │   └── SettingsViewModel.cs
│       └── Views/
│           ├── MainWindow.xaml                   # FluentWindow with Mica & NavigationView
│           ├── Converters/                       # Health status color & visibility converters
│           └── Pages/                            # Page views for all features
└── tests/
    └── CertSentry.Tests/                         # Comprehensive xUnit & FluentAssertions suite
```

- **Target Framework**: .NET 10 (`net10.0-windows7.0`)
- **UI Framework**: WPF with Lepo `WPF-UI` (4.3.0)
- **MVVM Toolkit**: `CommunityToolkit.Mvvm` (8.4.2)
- **DI & Host**: `Microsoft.Extensions.Hosting` (10.0.12)
- **Test Suite**: xUnit (2.9.3) + FluentAssertions (8.8.0)

---

## 🚀 Getting Started

### Prerequisites
- Windows 10 (version 1903 or later) or Windows 11
- .NET 10 or .NET 8 SDK

### Build & Run
```powershell
# Clone the repository
git clone https://github.com/your-org/CertSentry.git
cd CertSentry

# Build solution
dotnet build CertSentry.slnx

# Run all tests
dotnet test CertSentry.slnx

# Launch CertSentry
dotnet run --project src/CertSentry/CertSentry.csproj
```

---

## 🧪 Testing

CertSentry includes an extensive automated test suite covering:
- RSA 2048 & ECDSA P-256 certificate generation.
- Multi-SAN encoding and validation (DNS names, IPv4, IPv6 loopback).
- CA Basic Constraints and Leaf signing by custom Root CAs.
- PFX password-protected export and PEM export.
- Subject Alternative Name (SAN) wildcard and alias matching.
- ASP.NET Core `dotnet dev-certs` JSON parsing and duplicate version detection.
- Heuristic identification of development certificates.

```powershell
dotnet test CertSentry.slnx --logger "console;verbosity=normal"
```

---

## 📄 License

This project is licensed under the [MIT License](LICENSE).
