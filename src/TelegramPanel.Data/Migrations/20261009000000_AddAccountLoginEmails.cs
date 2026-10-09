using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace TelegramPanel.Data.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261009000000_AddAccountLoginEmails")]
public sealed class AddAccountLoginEmails : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "AccountLoginEmails",
            columns: table => new
            {
                AccountId = table.Column<int>(type: "INTEGER", nullable: false),
                ConfirmedEmail = table.Column<string>(type: "TEXT", nullable: true),
                ConfirmedPattern = table.Column<string>(type: "TEXT", nullable: true),
                PendingEmail = table.Column<string>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AccountLoginEmails", x => x.AccountId);
                table.ForeignKey("FK_AccountLoginEmails_Accounts_AccountId", x => x.AccountId,
                    "Accounts", "Id", onDelete: ReferentialAction.Cascade);
            });
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropTable("AccountLoginEmails");
}
