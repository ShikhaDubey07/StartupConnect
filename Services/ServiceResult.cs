namespace StartupConnect.Services;

/// <summary>Outcome of a user-initiated operation with a message suitable for showing to the user.</summary>
public sealed record ServiceResult(bool Succeeded, string Message)
{
    public static ServiceResult Ok(string message) => new(true, message);
    public static ServiceResult Fail(string message) => new(false, message);
}
