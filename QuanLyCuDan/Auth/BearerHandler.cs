using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using QuanLyCuDan.Controllers;
using QuanLyCuDan.Models;

namespace QuanLyCuDan.Auth;
public sealed class BearerHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder, TokenIssuer issuer, MyStockContext db) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var header = Request.Headers.Authorization.ToString();
        if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return AuthenticateResult.NoResult();
        try
        {
            var principal = issuer.Validate(header[7..]);
            if (!await BaseController.ValidSession(db, principal))
                return AuthenticateResult.Fail("Session revoked or expired.");
            return AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name));
        }
        catch (Exception e) when (e is SecurityTokenException or ArgumentException)
        {
            return AuthenticateResult.Fail("Invalid access token.");
        }
    }
}
