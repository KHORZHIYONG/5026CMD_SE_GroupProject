using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ResROMS.Migrations
{
    /// <inheritdoc />
    public partial class AddDiningTableManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Tables_DiningAreas_DiningAreaId",
                table: "Tables");

            migrationBuilder.AlterColumn<string>(
                name: "TableNumber",
                table: "Tables",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Tables",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<string>(
                name: "Shape",
                table: "Tables",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Square");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "DiningAreas",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.CreateIndex(
                name: "IX_Tables_TableNumber",
                table: "Tables",
                column: "TableNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DiningAreas_Name",
                table: "DiningAreas",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Tables_DiningAreas_DiningAreaId",
                table: "Tables",
                column: "DiningAreaId",
                principalTable: "DiningAreas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Tables_DiningAreas_DiningAreaId",
                table: "Tables");

            migrationBuilder.DropIndex(
                name: "IX_Tables_TableNumber",
                table: "Tables");

            migrationBuilder.DropIndex(
                name: "IX_DiningAreas_Name",
                table: "DiningAreas");

            migrationBuilder.DropColumn(
                name: "Shape",
                table: "Tables");

            migrationBuilder.AlterColumn<string>(
                name: "TableNumber",
                table: "Tables",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Tables",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "DiningAreas",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(80)",
                oldMaxLength: 80);

            migrationBuilder.AddForeignKey(
                name: "FK_Tables_DiningAreas_DiningAreaId",
                table: "Tables",
                column: "DiningAreaId",
                principalTable: "DiningAreas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
