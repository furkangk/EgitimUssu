using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgitimUssu.Modules.Payments.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PaymentTransactions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "payment_transactions",
                schema: "payments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PaymentRecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Method = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    OccurredOnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_transactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_payment_transactions_payment_records_PaymentRecordId",
                        column: x => x.PaymentRecordId,
                        principalSchema: "payments",
                        principalTable: "payment_records",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_payment_transactions_PaymentRecordId_OccurredOnUtc",
                schema: "payments",
                table: "payment_transactions",
                columns: new[] { "PaymentRecordId", "OccurredOnUtc" });

            // Eski tabloda UpdatedOnUtc yoktur. Gerçek tahsil tarihi önceliklidir;
            // tarih tutulmamışsa vade geri alınabilir tarihsel yedektir.
            migrationBuilder.Sql("""
                INSERT INTO payments.payment_transactions
                    ("Id", "PaymentRecordId", "Kind", "Amount", "Method", "OccurredOnUtc", "Note")
                SELECT "Id", "Id", 'Collection', "CollectedAmount", 'Migrated',
                    COALESCE("CollectedOnUtc", "DueDateUtc"), NULL
                FROM payments.payment_records WHERE "CollectedAmount" > 0;
                UPDATE payments.payment_records SET "Status" = CASE
                    WHEN "Status" = 'Cancelled' THEN 'Cancelled'
                    WHEN "CollectedAmount" >= "ExpectedAmount" THEN 'Paid'
                    WHEN "CollectedAmount" > 0 THEN 'PartiallyPaid'
                    ELSE 'Pending' END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "payment_transactions",
                schema: "payments");
        }
    }
}
