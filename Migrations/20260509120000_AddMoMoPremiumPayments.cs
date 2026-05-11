using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Web_Project.Models;

#nullable disable

namespace Wed_Project.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20260509120000_AddMoMoPremiumPayments")]
    public partial class AddMoMoPremiumPayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_PaymentTransactions_Status",
                table: "PaymentTransactions");

            migrationBuilder.AddColumn<DateTime>(
                name: "FailedAt",
                table: "PaymentTransactions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OrderId",
                table: "PaymentTransactions",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PayUrl",
                table: "PaymentTransactions",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PlanCode",
                table: "PaymentTransactions",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Premium");

            migrationBuilder.AddColumn<string>(
                name: "Provider",
                table: "PaymentTransactions",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "Mock");

            migrationBuilder.AddColumn<string>(
                name: "ProviderMessage",
                table: "PaymentTransactions",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProviderResultCode",
                table: "PaymentTransactions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderTransactionId",
                table: "PaymentTransactions",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequestId",
                table: "PaymentTransactions",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "PaymentTransactions",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "SYSUTCDATETIME()");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PaymentTransactions_Status",
                table: "PaymentTransactions",
                sql: "[Status] IN (N'Pending', N'Success', N'Paid', N'Failed', N'Cancelled')");

            migrationBuilder.CreateTable(
                name: "UserSubscriptions",
                columns: table => new
                {
                    UserSubscriptionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    PlanCode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    StartsAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    PaymentTransactionId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserSubscriptions", x => x.UserSubscriptionId);
                    table.ForeignKey(
                        name: "FK_UserSubscriptions_PaymentTransactions_PaymentTransactionId",
                        column: x => x.PaymentTransactionId,
                        principalTable: "PaymentTransactions",
                        principalColumn: "PaymentTransactionId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_UserSubscriptions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.NoAction);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactions_OrderId",
                table: "PaymentTransactions",
                column: "OrderId",
                unique: true,
                filter: "[OrderId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactions_RequestId",
                table: "PaymentTransactions",
                column: "RequestId",
                unique: true,
                filter: "[RequestId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactions_UserId_Status_CreatedAt",
                table: "PaymentTransactions",
                columns: new[] { "UserId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_UserSubscriptions_PaymentTransactionId",
                table: "UserSubscriptions",
                column: "PaymentTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_UserSubscriptions_UserId_PlanCode_IsActive_ExpiresAt",
                table: "UserSubscriptions",
                columns: new[] { "UserId", "PlanCode", "IsActive", "ExpiresAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserSubscriptions");

            migrationBuilder.DropIndex(
                name: "IX_PaymentTransactions_OrderId",
                table: "PaymentTransactions");

            migrationBuilder.DropIndex(
                name: "IX_PaymentTransactions_RequestId",
                table: "PaymentTransactions");

            migrationBuilder.DropIndex(
                name: "IX_PaymentTransactions_UserId_Status_CreatedAt",
                table: "PaymentTransactions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PaymentTransactions_Status",
                table: "PaymentTransactions");

            migrationBuilder.DropColumn(
                name: "FailedAt",
                table: "PaymentTransactions");

            migrationBuilder.DropColumn(
                name: "OrderId",
                table: "PaymentTransactions");

            migrationBuilder.DropColumn(
                name: "PayUrl",
                table: "PaymentTransactions");

            migrationBuilder.DropColumn(
                name: "PlanCode",
                table: "PaymentTransactions");

            migrationBuilder.DropColumn(
                name: "Provider",
                table: "PaymentTransactions");

            migrationBuilder.DropColumn(
                name: "ProviderMessage",
                table: "PaymentTransactions");

            migrationBuilder.DropColumn(
                name: "ProviderResultCode",
                table: "PaymentTransactions");

            migrationBuilder.DropColumn(
                name: "ProviderTransactionId",
                table: "PaymentTransactions");

            migrationBuilder.DropColumn(
                name: "RequestId",
                table: "PaymentTransactions");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "PaymentTransactions");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PaymentTransactions_Status",
                table: "PaymentTransactions",
                sql: "[Status] IN (N'Pending', N'Success', N'Failed', N'Cancelled')");
        }
    }
}
