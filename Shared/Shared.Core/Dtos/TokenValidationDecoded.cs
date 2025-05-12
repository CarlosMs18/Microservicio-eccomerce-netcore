namespace Shared.Core.Dtos
{
    public class TokenValidationDecoded
    {
        public bool IsValid { get; set; }
        public string? UserId { get; set; }
        public string? Email { get; set; }
        public List<string> Roles { get; set; } = new();
        public Dictionary<string, string> Claims { get; set; } = new();
    }
}
