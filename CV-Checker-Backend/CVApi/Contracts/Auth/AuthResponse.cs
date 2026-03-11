namespace CVApi.Contracts.Auth;

public sealed class AuthResponse
{
    public string Token { get; set; } = "";
    public DateTime ExpiresAtUtc { get; set; }

    public Guid UserId { get; set; }
    public string? Name { get; set; }
    public string? Email { get; set; }
}


