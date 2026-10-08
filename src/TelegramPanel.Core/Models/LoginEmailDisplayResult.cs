namespace TelegramPanel.Core.Models;

public sealed record LoginEmailDisplayResult(
    bool Success,
    string? Error,
    bool HasLoginEmail,
    string? LoginEmailPattern,
    string? LoginEmail,
    string VerificationStatus);
