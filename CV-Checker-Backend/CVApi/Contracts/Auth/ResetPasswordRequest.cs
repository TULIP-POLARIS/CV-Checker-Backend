namespace CVApi.Contracts.Auth;

public sealed class ResetPasswordRequest
{
    public string NewPassword { get; set; } = "";
}
