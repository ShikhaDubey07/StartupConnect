using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using StartupConnect.Models;

namespace StartupConnect.Infrastructure;

/// <summary>
/// Blocks suspended accounts (<see cref="ApplicationUser.IsActive"/> == false) from signing in and
/// invalidates their existing cookies on the next security-stamp validation.
/// </summary>
public sealed class AppSignInManager : SignInManager<ApplicationUser>
{
    public AppSignInManager(
        UserManager<ApplicationUser> userManager,
        IHttpContextAccessor contextAccessor,
        IUserClaimsPrincipalFactory<ApplicationUser> claimsFactory,
        IOptions<IdentityOptions> optionsAccessor,
        ILogger<SignInManager<ApplicationUser>> logger,
        IAuthenticationSchemeProvider schemes,
        IUserConfirmation<ApplicationUser> confirmation)
        : base(userManager, contextAccessor, claimsFactory, optionsAccessor, logger, schemes, confirmation)
    {
    }

    public override async Task<bool> CanSignInAsync(ApplicationUser user)
    {
        if (!user.IsActive) return false;
        return await base.CanSignInAsync(user);
    }

    public override async Task<ApplicationUser?> ValidateSecurityStampAsync(System.Security.Claims.ClaimsPrincipal? principal)
    {
        var user = await base.ValidateSecurityStampAsync(principal);
        return user is { IsActive: true } ? user : null;
    }
}
