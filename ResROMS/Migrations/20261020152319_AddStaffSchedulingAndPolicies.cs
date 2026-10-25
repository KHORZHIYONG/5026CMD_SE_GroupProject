using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ResROMS.Migrations
{
    /// <inheritdoc />
    public partial class AddStaffSchedulingAndPolicies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MaximumShiftHours",
                table: "SystemSettings",
                type: "int",
                nullable: false,
                defaultValue: 12);

            migrationBuilder.AddColumn<int>(
                name: "MaximumWeeklyHours",
                table: "SystemSettings",
                type: "int",
                nullable: false,
                defaultValue: 60);

            migrationBuilder.AddColumn<int>(
                name: "MinimumRestHours",
                table: "SystemSettings",
                type: "int",
                nullable: false,
                defaultValue: 8);

            migrationBuilder.CreateTable(
                name: "StaffShifts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StaffUserId = table.Column<int>(type: "int", nullable: false),
                    StartLocal = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndLocal = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffShifts", x => x.Id);
                    table.CheckConstraint("CK_StaffShifts_Interval", "[EndLocal] > [StartLocal]");
                    table.ForeignKey(
                        name: "FK_StaffShifts_Users_StaffUserId",
                        column: x => x.StaffUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_SystemSettings_RestMin",
                table: "SystemSettings",
                sql: "[MinimumRestHours] BETWEEN 0 AND 24");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SystemSettings_ShiftMax",
                table: "SystemSettings",
                sql: "[MaximumShiftHours] BETWEEN 1 AND 16");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SystemSettings_WeeklyMax",
                table: "SystemSettings",
                sql: "[MaximumWeeklyHours] BETWEEN 8 AND 80");

            migrationBuilder.CreateIndex(
                name: "IX_StaffShifts_StaffUserId_StartLocal_EndLocal",
                table: "StaffShifts",
                columns: new[] { "StaffUserId", "StartLocal", "EndLocal" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StaffShifts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SystemSettings_RestMin",
                table: "SystemSettings");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SystemSettings_ShiftMax",
                table: "SystemSettings");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SystemSettings_WeeklyMax",
                table: "SystemSettings");

            migrationBuilder.DropColumn(
                name: "MaximumShiftHours",
                table: "SystemSettings");

            migrationBuilder.DropColumn(
                name: "MaximumWeeklyHours",
                table: "SystemSettings");

            migrationBuilder.DropColumn(
                name: "MinimumRestHours",
                table: "SystemSettings");
        }
    }
}
