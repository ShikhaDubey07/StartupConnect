using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace StartupConnect.Infrastructure;

/// <summary>Options for <see cref="PasswordResetTokenProvider{TUser}"/> (lifespan from Auth:PasswordResetTokenHours).</summary>
public sealed class PasswordResetTokenProviderOptions : DataProtectionTokenProviderOptions
{
    public const string ProviderName = "PasswordReset";
    public const string ConfigKey = "Auth:PasswordResetTokenHours";
    public const int DefaultHours = 2;

    public PasswordResetTokenProviderOptions()
    {
        Name = ProviderName;
        TokenLifespan = TimeSpan.FromHours(DefaultHours);
    }
}

/// <summary>
/// Password reset tokens get their own, short lifespan (default 2 hours) instead of the 24 hours used for
/// email confirmation. Tokens are single-use: a successful reset changes the security stamp.
/// </summary>
public sealed class PasswordResetTokenProvider<TUser> : DataProtectorTokenProvider<TUser> where TUser : class
{
    public PasswordResetTokenProvider(IDataProtectionProvider dataProtectionProvider,
        IOptions<PasswordResetTokenProviderOptions> options, ILogger<DataProtectorTokenProvider<TUser>> logger)
        : base(dataProtectionProvider, options, logger)
    {
    }
}
