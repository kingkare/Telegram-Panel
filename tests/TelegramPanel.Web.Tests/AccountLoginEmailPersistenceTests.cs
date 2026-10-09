using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using TelegramPanel.Core.Services.Telegram;
using TelegramPanel.Data;
using TelegramPanel.Data.Entities;
using Xunit;

namespace TelegramPanel.Web.Tests;

public sealed class AccountLoginEmailPersistenceTests
{
    private const string Email = "owner@example.com";
    private const string Pattern = "o***r@example.com";

    [Fact]
    public async Task 确认邮箱跨服务重建保留且掩码一致显示完整地址()
    {
        await using var fixture = await Fixture.CreateAsync();
        await ConfirmAsync(fixture.Service);
        var restarted = new AccountLoginEmailService(fixture.Provider.GetRequiredService<IServiceScopeFactory>());
        var result = await restarted.GetAsync(1, () => Remote(true, Pattern));
        Assert.Equal(Email, result.LoginEmail);
        Assert.Equal("verified", result.VerificationStatus);
    }

    [Theory]
    [InlineData(true, "n***w@example.com")]
    [InlineData(false, null)]
    public async Task 云端修改或移除后旧地址永久失效(bool hasEmail, string? pattern)
    {
        await using var fixture = await Fixture.CreateAsync();
        await ConfirmAsync(fixture.Service);
        var changed = await fixture.Service.GetAsync(1, () => Remote(hasEmail, pattern));
        Assert.Null(changed.LoginEmail);
        var failed = await fixture.Service.GetAsync(1, FailedRemote);
        Assert.Null(failed.LoginEmail);
        var reverted = await fixture.Service.GetAsync(1, () => Remote(true, Pattern));
        Assert.Null(reverted.LoginEmail);
    }

    [Fact]
    public async Task 云端请求失败保留已确认地址但标记未验证()
    {
        await using var fixture = await Fixture.CreateAsync();
        await ConfirmAsync(fixture.Service);
        var result = await fixture.Service.GetAsync(1, FailedRemote);
        Assert.False(result.Success);
        Assert.Equal(Email, result.LoginEmail);
        Assert.Equal("unverified", result.VerificationStatus);
        Assert.Equal(Email, (await fixture.Service.GetAsync(1, () => Remote(true, Pattern))).LoginEmail);
    }

    [Fact]
    public async Task 发码与验证码失败不能覆盖已确认地址()
    {
        await using var fixture = await Fixture.CreateAsync();
        await ConfirmAsync(fixture.Service);
        await fixture.Service.SendAsync(1, "new@example.com", () => Task.FromResult<string?>("n*w@example.com"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.ConfirmAsync(1,
            () => throw new InvalidOperationException("验证码错误"), () => Remote(true, Pattern)));
        Assert.Equal(Email, (await fixture.Service.GetAsync(1, () => Remote(true, Pattern))).LoginEmail);
    }

    [Fact]
    public async Task 待验证邮箱不显示且官方确认其他地址时不能误信本地地址()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.Service.SendAsync(1, Email, () => Task.FromResult<string?>(Pattern));
        Assert.Null((await fixture.Service.GetAsync(1, () => Remote(true, Pattern))).LoginEmail);
        await fixture.Service.ConfirmAsync(1, () => Task.FromResult<string?>("other@example.com"), () => Remote(true, Pattern));
        Assert.Null((await fixture.Service.GetAsync(1, () => Remote(true, Pattern))).LoginEmail);
    }

    [Fact]
    public async Task 确认后读取掩码失败仍持久保存但不能将后续掩码当作确认基准()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.Service.SendAsync(1, Email, () => Task.FromResult<string?>(Pattern));
        await fixture.Service.ConfirmAsync(1, () => Task.FromResult<string?>(Email), FailedRemote);
        var failed = await fixture.Service.GetAsync(1, FailedRemote);
        Assert.Equal(Email, failed.LoginEmail);
        Assert.Equal("unverified", failed.VerificationStatus);
        var later = await fixture.Service.GetAsync(1, () => Remote(true, Pattern));
        Assert.Equal(Email, later.LoginEmail);
        Assert.Equal("unverified", later.VerificationStatus);
    }

    [Fact]
    public async Task 云端暂缺掩码不清理本地邮箱且恢复后继续校验()
    {
        await using var fixture = await Fixture.CreateAsync();
        await ConfirmAsync(fixture.Service);
        var missing = await fixture.Service.GetAsync(1, () => Remote(true, null));
        Assert.Equal(Email, missing.LoginEmail);
        Assert.Equal("unverified", missing.VerificationStatus);
        Assert.Equal("verified", (await fixture.Service.GetAsync(1, () => Remote(true, Pattern))).VerificationStatus);
    }

    [Fact]
    public async Task 缺少确认基准但云端明确不匹配时仍应永久清理旧地址()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.Service.SendAsync(1, Email, () => Task.FromResult<string?>(Pattern));
        await fixture.Service.ConfirmAsync(1, () => Task.FromResult<string?>(Email), FailedRemote);
        var changed = await fixture.Service.GetAsync(1, () => Remote(true, "x***z@other.com"));
        Assert.Null(changed.LoginEmail);
        Assert.Null((await fixture.Service.GetAsync(1, FailedRemote)).LoginEmail);
    }

    [Fact]
    public async Task 确认后云端掩码与已验证邮箱不匹配不能建立错误基准()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.Service.SendAsync(1, Email, () => Task.FromResult<string?>(Pattern));
        await fixture.Service.ConfirmAsync(1, () => Task.FromResult<string?>(Email), () => Remote(true, "x***z@example.com"));
        Assert.Null((await fixture.Service.GetAsync(1, () => Remote(true, "x***z@example.com"))).LoginEmail);
    }

    [Fact]
    public async Task 同账号查询等待确认完成且不阻塞其他账号()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.Service.SendAsync(1, Email, () => Task.FromResult<string?>(Pattern));
        var verificationStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseVerification = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var confirm = fixture.Service.ConfirmAsync(1, () =>
        {
            verificationStarted.SetResult();
            return releaseVerification.Task;
        }, () => Remote(true, Pattern));
        await verificationStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var readStarted = false;
        var read = fixture.Service.GetAsync(1, () => { readStarted = true; return Remote(true, Pattern); });
        try
        {
            await fixture.Service.GetAsync(2, () => Remote(false, null)).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(readStarted);
        }
        finally { releaseVerification.TrySetResult(Email); }
        await confirm.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(Email, (await read.WaitAsync(TimeSpan.FromSeconds(5))).LoginEmail);
    }

    [Fact]
    public async Task 迁移保留已有账号且删除账号级联清理邮箱()
    {
        await using var fixture = await Fixture.CreateAsync(migrateFromPrevious: true);
        await ConfirmAsync(fixture.Service);
        using var scope = fixture.Provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal("1001", (await db.Accounts.SingleAsync(x => x.Id == 1)).Phone);
        await db.Accounts.Where(x => x.Id == 1).ExecuteDeleteAsync();
        Assert.Empty(await db.AccountLoginEmails.ToListAsync());
    }

    private static async Task ConfirmAsync(AccountLoginEmailService service)
    {
        await service.SendAsync(1, Email, () => Task.FromResult<string?>(Pattern));
        await service.ConfirmAsync(1, () => Task.FromResult<string?>(Email), () => Remote(true, Pattern));
    }

    private static Task<(bool Success, string? Error, bool HasLoginEmail, string? LoginEmailPattern)> Remote(bool hasEmail, string? pattern) =>
        Task.FromResult<(bool, string?, bool, string?)>((true, null, hasEmail, pattern));
    private static Task<(bool Success, string? Error, bool HasLoginEmail, string? LoginEmailPattern)> FailedRemote() =>
        Task.FromResult<(bool, string?, bool, string?)>((false, "连接失败", false, null));

    private sealed class Fixture(SqliteConnection connection, ServiceProvider provider) : IAsyncDisposable
    {
        public ServiceProvider Provider => provider;
        public AccountLoginEmailService Service { get; } = provider.GetRequiredService<AccountLoginEmailService>();

        public static async Task<Fixture> CreateAsync(bool migrateFromPrevious = false)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var services = new ServiceCollection();
            services.AddDbContext<AppDbContext>(options => options.UseSqlite(connection));
            services.AddSingleton<AccountLoginEmailService>();
            var provider = services.BuildServiceProvider();
            using var scope = provider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            if (migrateFromPrevious)
                await db.Database.GetService<IMigrator>().MigrateAsync("20260826093000_AddBatchTaskNextEligibleAt");
            else
                await db.Database.EnsureCreatedAsync();
            db.Accounts.AddRange(Enumerable.Range(1, 2).Select(id => new Account
            {
                Id = id, Phone = (1000 + id).ToString(), ApiHash = "hash", SessionPath = $"{id}.session"
            }));
            await db.SaveChangesAsync();
            if (migrateFromPrevious) await db.Database.MigrateAsync();
            return new(connection, provider);
        }

        public async ValueTask DisposeAsync()
        {
            await provider.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
