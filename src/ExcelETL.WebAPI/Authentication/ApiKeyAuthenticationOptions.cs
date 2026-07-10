using Microsoft.AspNetCore.Authentication;

namespace ExcelETL.WebAPI.Authentication;

public class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
    public string ApiKey { get; set; } = string.Empty;
}
