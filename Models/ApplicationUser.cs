using Microsoft.AspNetCore.Identity;

public class ApplicationUser : IdentityUser
{
    public int CompanyId { get; set; }
    // Add other custom fields as needed
}