using System.Collections.Generic;

namespace CertSentry.Services;

public class SnippetItem
{
    public string Title { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Language { get; set; } = "powershell";
    public string Code { get; set; } = string.Empty;
    public string Explanation { get; set; } = string.Empty;
}

public interface IEnvironmentSnippetService
{
    List<SnippetItem> GenerateSnippets(string caCertPath, string? privateKeyPath = null, int port = 5001);
}

public class EnvironmentSnippetService : IEnvironmentSnippetService
{
    public List<SnippetItem> GenerateSnippets(string caCertPath, string? privateKeyPath = null, int port = 5001)
    {
        var safeCertPath = string.IsNullOrWhiteSpace(caCertPath) ? "C:\\certs\\localhost-ca.crt" : caCertPath.Trim();
        var safeKeyPath = string.IsNullOrWhiteSpace(privateKeyPath) ? "C:\\certs\\localhost-ca.key" : privateKeyPath.Trim();

        return new List<SnippetItem>
        {
            new()
            {
                Title = "Node.js (PowerShell)",
                Category = "Node.js",
                Language = "powershell",
                Code = $"$env:NODE_EXTRA_CA_CERTS=\"{safeCertPath}\"",
                Explanation = "Instructs Node.js (v7.3.0+) to trust the specified CA bundle alongside built-in Mozilla roots."
            },
            new()
            {
                Title = "Node.js (CMD / .env)",
                Category = "Node.js",
                Language = "batch",
                Code = $"set NODE_EXTRA_CA_CERTS={safeCertPath}",
                Explanation = "Sets the environment variable for classic Windows Command Prompt or scripts."
            },
            new()
            {
                Title = "Git for Windows (CLI)",
                Category = "Git",
                Language = "powershell",
                Code = $"git config --global http.sslCAInfo \"{safeCertPath}\"",
                Explanation = "Configures Git to trust your local Root CA for localhost Git servers and local clones."
            },
            new()
            {
                Title = "cURL (Command Line)",
                Category = "cURL",
                Language = "powershell",
                Code = $"curl --cacert \"{safeCertPath}\" https://localhost:{port}/",
                Explanation = "Verifies the endpoint using curl while specifying your custom Root CA explicitly."
            },
            new()
            {
                Title = "Python Requests (PowerShell)",
                Category = "Python",
                Language = "powershell",
                Code = $"$env:REQUESTS_CA_BUNDLE=\"{safeCertPath}\"",
                Explanation = "Instructs Python requests, urllib3, and httpx to trust the local CA bundle."
            },
            new()
            {
                Title = "Docker Container Trust (Dockerfile)",
                Category = "Docker",
                Language = "dockerfile",
                Code = $"# Copy CA into Debian/Ubuntu image trust store\nCOPY ca.crt /usr/local/share/ca-certificates/certsentry-ca.crt\nRUN update-ca-certificates",
                Explanation = "Installs the Root CA into the container's OS certificate authority store during build."
            },
            new()
            {
                Title = "WSL (Windows Subsystem for Linux)",
                Category = "WSL",
                Language = "bash",
                Code = $"sudo cp \"{safeCertPath.Replace('\\', '/')}\" /usr/local/share/ca-certificates/localhost-ca.crt\nsudo update-ca-certificates",
                Explanation = "Syncs and trusts your Windows development certificate inside WSL (Ubuntu / Debian)."
            },
            new()
            {
                Title = "Vite Dev Server (vite.config.ts)",
                Category = "Vite",
                Language = "typescript",
                Code = $"import {{ defineConfig }} from 'vite';\nimport fs from 'fs';\n\nexport default defineConfig({{\n  server: {{\n    https: {{\n      key: fs.readFileSync('{safeKeyPath.Replace('\\', '/')}'),\n      cert: fs.readFileSync('{safeCertPath.Replace('\\', '/')}'),\n    }},\n    port: {port}\n  }}\n}});",
                Explanation = "Configures Vite dev server with explicit SSL/TLS key and certificate files."
            }
        };
    }
}
