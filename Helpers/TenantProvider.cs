using System.Security.Claims;
using Microsoft.AspNetCore.Http;

public interface ITenantProvider
{
    int CompanyId { get; }
}

public class HttpContextTenantProvider : ITenantProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpContextTenantProvider(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public int CompanyId
    {
        get
        {
            var user = _httpContextAccessor.HttpContext?.User;
            if (user is null) return 0;

            // Backward-compatible: prefer tenantId, fallback to companyId
            var claimValue = user.FindFirst("tenantId")?.Value ?? user.FindFirst("companyId")?.Value;
            return int.TryParse(claimValue, out var id) ? id : 0;
        }
    }
}

