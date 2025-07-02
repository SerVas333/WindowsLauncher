using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WindowsLauncher.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUserAuthenticationFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Applications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    ExecutablePath = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    Arguments = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    IconPath = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    Category = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    RequiredGroups = table.Column<string>(type: "TEXT", nullable: false),
                    MinimumRole = table.Column<int>(type: "INTEGER", nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Applications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Username = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Action = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ApplicationName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Details = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Success = table.Column<bool>(type: "INTEGER", nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    ComputerName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    IPAddress = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    UserAgent = table.Column<string>(type: "TEXT", nullable: false),
                    IpAddress = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Username = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    Groups = table.Column<string>(type: "TEXT", nullable: false),
                    Role = table.Column<int>(type: "INTEGER", nullable: false),
                    LastLogin = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastActivityAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsServiceAccount = table.Column<bool>(type: "INTEGER", nullable: false),
                    PasswordHash = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Salt = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    FailedLoginAttempts = table.Column<int>(type: "INTEGER", nullable: false),
                    IsLocked = table.Column<bool>(type: "INTEGER", nullable: false),
                    LockoutEnd = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Username = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Theme = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    AccentColor = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    TileSize = table.Column<int>(type: "INTEGER", nullable: false),
                    ShowCategories = table.Column<bool>(type: "INTEGER", nullable: false),
                    DefaultCategory = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    HiddenCategories = table.Column<string>(type: "TEXT", nullable: false),
                    AutoRefresh = table.Column<bool>(type: "INTEGER", nullable: false),
                    RefreshIntervalMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    ShowDescriptions = table.Column<bool>(type: "INTEGER", nullable: false),
                    LastModified = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserSettings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Applications_Category",
                table: "Applications",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_Applications_IsEnabled",
                table: "Applications",
                column: "IsEnabled");

            migrationBuilder.CreateIndex(
                name: "IX_Applications_Name",
                table: "Applications",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Action",
                table: "AuditLogs",
                column: "Action");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Timestamp",
                table: "AuditLogs",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Username",
                table: "AuditLogs",
                column: "Username");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Username_Timestamp",
                table: "AuditLogs",
                columns: new[] { "Username", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_Users_IsActive",
                table: "Users",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Users_IsServiceAccount",
                table: "Users",
                column: "IsServiceAccount");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Username",
                table: "Users",
                column: "Username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserSettings_Username",
                table: "UserSettings",
                column: "Username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Applications");

            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "UserSettings");
        }
    }
}
