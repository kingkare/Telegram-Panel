using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TelegramPanel.Core.Models;
using TelegramPanel.Data;
using TelegramPanel.Data.Entities;

namespace TelegramPanel.Core.Services.Telegram;

/// <summary>按账号串行执行云端操作与本地保存，避免旧查询覆盖新确认的邮箱。</summary>
public sealed class AccountLoginEmailService(IServiceScopeFactory scopeFactory)
{
    private readonly ConcurrentDictionary<int, SemaphoreSlim> _gates = new();

    public async Task<string?> SendAsync(int accountId, string email, Func<Task<string?>> send,
        CancellationToken cancellationToken = default)
    {
        var gate = _gates.GetOrAdd(accountId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var row = await GetOrCreateAsync(db, accountId, cancellationToken);
            var pattern = await send();
            row.PendingEmail = email;
            // 云端已经发送验证码，不能因浏览器取消请求丢失待确认地址。
            await db.SaveChangesAsync(CancellationToken.None);
            return pattern;
        }
        finally { gate.Release(); }
    }

    public async Task ConfirmAsync(int accountId, Func<Task<string?>> verify,
        Func<Task<(bool Success, string? Error, bool HasLoginEmail, string? LoginEmailPattern)>> read,
        CancellationToken cancellationToken = default)
    {
        var gate = _gates.GetOrAdd(accountId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var row = await GetOrCreateAsync(db, accountId, cancellationToken);
            var email = (await verify())?.Trim();
            // 必须使用 Telegram 确认返回的真实地址，不能把最近发送的地址直接升级为已确认。
            var matches = !string.IsNullOrWhiteSpace(email) && !string.IsNullOrWhiteSpace(row.PendingEmail)
                && string.Equals(email, row.PendingEmail, StringComparison.OrdinalIgnoreCase);
            row.ConfirmedEmail = matches ? email : null;
            row.ConfirmedPattern = null;
            row.PendingEmail = null;
            await db.SaveChangesAsync(CancellationToken.None);

            var remote = await read();
            if (remote.Success)
            {
                if (remote.HasLoginEmail)
                {
                    var pattern = NormalizePattern(remote.LoginEmailPattern);
                    if (row.ConfirmedEmail != null && pattern != null)
                    {
                        if (PatternMatchesEmail(pattern, row.ConfirmedEmail))
                            row.ConfirmedPattern = pattern;
                        else
                            row.ConfirmedEmail = null;
                    }
                }
                else
                    row.ConfirmedEmail = null;
                await db.SaveChangesAsync(CancellationToken.None);
            }
        }
        finally { gate.Release(); }
    }

    public async Task<LoginEmailDisplayResult> GetAsync(int accountId,
        Func<Task<(bool Success, string? Error, bool HasLoginEmail, string? LoginEmailPattern)>> read,
        CancellationToken cancellationToken = default)
    {
        var gate = _gates.GetOrAdd(accountId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var row = await db.AccountLoginEmails.SingleOrDefaultAsync(x => x.AccountId == accountId, cancellationToken);
            var remote = await read();
            if (!remote.Success)
                return new(false, remote.Error, row?.ConfirmedEmail != null, row?.ConfirmedPattern,
                    row?.ConfirmedEmail, row?.ConfirmedEmail != null ? "unverified" : "unavailable");

            var pattern = NormalizePattern(remote.LoginEmailPattern);
            if (row?.ConfirmedEmail != null && (!remote.HasLoginEmail
                || (pattern != null && (row.ConfirmedPattern != null
                    ? !string.Equals(row.ConfirmedPattern, pattern, StringComparison.Ordinal)
                    : !PatternMatchesEmail(pattern, row.ConfirmedEmail)))))
            {
                // 云端明确变更后持久失效，后续请求失败或掩码恢复也不能复活旧邮箱。
                row.ConfirmedEmail = null;
                row.ConfirmedPattern = null;
                await db.SaveChangesAsync(CancellationToken.None);
            }
            var verified = row?.ConfirmedEmail != null && row.ConfirmedPattern != null
                && remote.HasLoginEmail && string.Equals(row.ConfirmedPattern, pattern, StringComparison.Ordinal);
            return new(true, null, remote.HasLoginEmail, pattern,
                row?.ConfirmedEmail, verified ? "verified" : row?.ConfirmedEmail != null ? "unverified" : "unavailable");
        }
        finally { gate.Release(); }
    }

    private static string? NormalizePattern(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool PatternMatchesEmail(string pattern, string email) =>
        Regex.IsMatch(email, "\\A" + Regex.Escape(pattern).Replace("\\*", ".*") + "\\z",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    private static async Task<AccountLoginEmail> GetOrCreateAsync(AppDbContext db, int accountId, CancellationToken cancellationToken)
    {
        var row = await db.AccountLoginEmails.SingleOrDefaultAsync(x => x.AccountId == accountId, cancellationToken);
        if (row != null) return row;
        row = new AccountLoginEmail { AccountId = accountId };
        db.AccountLoginEmails.Add(row);
        return row;
    }
}
