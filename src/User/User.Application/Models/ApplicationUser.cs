using Microsoft.AspNetCore.Identity;
namespace User.Application.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string? FirstName { get; set; } = string.Empty;
        public string? LastName { get; set; } = string.Empty;
        public string? Avatar { get; set; } = string.Empty;
        public string? Avatar_large { get; set; } = string.Empty;
        public int? CountrieId { get; set; }
        public int? CityId { get; set; }
        public string? Company { get; set; } = string.Empty;
        public string? CompanySite { get; set; } = string.Empty;
        public int? TimeZoneId { get; set; }
        public virtual ICollection<ApplicationUserRole> UserRoles { get; set; } = new List<ApplicationUserRole>();


    }
}
