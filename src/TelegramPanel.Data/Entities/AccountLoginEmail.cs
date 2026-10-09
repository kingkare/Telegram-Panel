namespace TelegramPanel.Data.Entities;

/// <summary>面板确认过的登录邮箱；待验证地址不能作为账号当前邮箱展示。</summary>
public sealed class AccountLoginEmail
{
    public int AccountId { get; set; }
    public string? ConfirmedEmail { get; set; }
    public string? ConfirmedPattern { get; set; }
    public string? PendingEmail { get; set; }
}
