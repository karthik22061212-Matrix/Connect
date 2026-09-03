namespace Connect.Application.Common.Models;

public class TurnCredentialsDto
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public int Ttl { get; set; }
    public string[] Uris { get; set; } = Array.Empty<string>();
}
