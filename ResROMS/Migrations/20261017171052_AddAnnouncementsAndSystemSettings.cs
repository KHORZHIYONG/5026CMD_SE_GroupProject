using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ResROMS.Migrations
{
    /// <inheritdoc />
    public partial class AddAnnouncementsAndSystemSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Announcements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(140)", maxLength: 140, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Audience = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    StartsUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndsUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsPublished = table.Column<bool>(type: "bit", nullable: false),
                    IsArchived = table.Column<bool>(type: "bit", nullable: false),
                    FirstPublishedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Announcements", x => x.Id);
                    table.CheckConstraint("CK_Announcements_Audience", "[Audience] IN ('All','Customer','Staff')");
                    table.CheckConstraint("CK_Announcements_DateRange", "[EndsUtc] IS NULL OR [EndsUtc] > [StartsUtc]");
                    table.ForeignKey(
                        name: "FK_Announcements_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SystemSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    CustomerBookingsEnabled = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    MinimumBookingNoticeMinutes = table.Column<int>(type: "int", nullable: false, defaultValue: 15),
                    MaximumBookingAdvanceDays = table.Column<int>(type: "int", nullable: false, defaultValue: 90),
                    UpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemSettings", x => x.Id);
                    table.CheckConstraint("CK_SystemSettings_MaxAdvance", "[MaximumBookingAdvanceDays] BETWEEN 1 AND 365");
                    table.CheckConstraint("CK_SystemSettings_MinNotice", "[MinimumBookingNoticeMinutes] BETWEEN 0 AND 1440");
                    table.CheckConstraint("CK_SystemSettings_Singleton", "[Id] = 1");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Announcements_CreatedByUserId",
                table: "Announcements",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Announcements_IsPublished_IsArchived_StartsUtc",
                table: "Announcements",
                columns: new[] { "IsPublished", "IsArchived", "StartsUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Announcements");

            migrationBuilder.DropTable(
                name: "SystemSettings");
        }
    }
}
