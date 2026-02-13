namespace McpTextEditor;

public class TransportConfig
{
    public string Mode { get; set; } = "Stdio";
    public HttpTransportConfig Http { get; set; } = new();
}

public class HttpTransportConfig
{
    public string Url { get; set; } = "https://localhost:5000";
    public string McpPath { get; set; } = "/mcp";
    public string ApiKey { get; set; } = string.Empty;
    public CertificateConfig Certificate { get; set; } = new();
}

public class CertificateConfig
{
    public string Path { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
