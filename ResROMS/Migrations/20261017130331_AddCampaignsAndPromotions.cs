using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ResROMS.Migrations
{
    /// <inheritdoc />
    public partial class AddCampaignsAndPromotions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Campaigns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    PromoCode = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: true),
                    Type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    DiscountType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    DiscountValue = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    StartDate = table.Column<DateTime>(type: "date", nullable: false),
                    EndDate = table.Column<DateTime>(type: "date", nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    UsageCount = table.Column<int>(type: "int", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Campaigns", x => x.Id);
                    table.CheckConstraint("CK_Campaigns_DateRange", "[EndDate] >= [StartDate]");
                    table.CheckConstraint("CK_Campaigns_DiscountType", "[DiscountType] IN ('None', 'Percentage', 'FixedAmount')");
                    table.CheckConstraint("CK_Campaigns_DiscountValue", "([DiscountType] = 'None' AND [DiscountValue] = 0) OR ([DiscountType] = 'Percentage' AND [DiscountValue] > 0 AND [DiscountValue] <= 100) OR ([DiscountType] = 'FixedAmount' AND [DiscountValue] > 0)");
                    table.CheckConstraint("CK_Campaigns_Type", "[Type] IN ('Discount', 'Happy Hour', 'Seasonal')");
                    table.CheckConstraint("CK_Campaigns_UsageCount", "[UsageCount] >= 0");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Campaigns_PromoCode",
                table: "Campaigns",
                column: "PromoCode",
                unique: true,
                filter: "[PromoCode] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Campaigns");
        }
    }
}
