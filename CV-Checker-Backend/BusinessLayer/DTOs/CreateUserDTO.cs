namespace BusinessLogic.DTOs
{
    public class CreateUserDTO
    {
        public string Name { get; set; } = default!;
        public string Email { get; set; } = default!;
        public string? PhoneNumber { get; set; }
        public string Password { get; set; } = default!;
    }
}
