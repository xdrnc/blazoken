namespace OAuthDemo.ResourceApi.Models;

public class ClientData
{
    public int Id { get; set; }
    public string ClientId { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}