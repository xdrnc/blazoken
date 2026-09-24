namespace OAuthDemo.Shared.Constants;

public static class Scopes
{
    public const string ApiRead = "api.read";
    public const string ApiWrite = "api.write";
    public const string ApiAdmin = "api.admin";
    
    public static readonly string[] All = [ApiRead, ApiWrite, ApiAdmin];
}