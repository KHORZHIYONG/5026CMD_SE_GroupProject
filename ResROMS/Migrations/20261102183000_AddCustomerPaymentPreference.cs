using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ResROMS.Data;

#nullable disable
namespace ResROMS.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261102183000_AddCustomerPaymentPreference")]
public sealed class AddCustomerPaymentPreference : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(name: "AppliedPromoCode", table: "Orders",
            type: "nvarchar(24)", maxLength: 24, nullable: true);
        migrationBuilder.AddColumn<string>(name: "PaymentPreference", table: "Orders",
            type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "None");
        migrationBuilder.AddColumn<string>(name: "PaymentReference", table: "Orders",
            type: "nvarchar(100)", maxLength: 100, nullable: true);
        migrationBuilder.AddCheckConstraint(name: "CK_Orders_PaymentPreference", table: "Orders",
            sql: "[PaymentPreference] IN ('None','Cash','Card','BankTransfer')");
    }
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(name: "CK_Orders_PaymentPreference", table: "Orders");
        migrationBuilder.DropColumn(name: "PaymentReference", table: "Orders");
        migrationBuilder.DropColumn(name: "AppliedPromoCode", table: "Orders");
        migrationBuilder.DropColumn(name: "PaymentPreference", table: "Orders");
    }
}
