using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgitimUssu.Modules.Payments.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SinglePendingDeclaration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_parent_payment_declarations_PaymentRecordId",
                schema: "payments",
                table: "parent_payment_declarations");

            // xmin PostgreSQL sistem kolonudur; scaffold işlemi sağlayıcı SQL'inde
            // etkisizdir (ileri/geri SQL kanıtlandı). Veri temizliği indeksin önündedir.
            migrationBuilder.Sql("""
                WITH ranked AS (
                    SELECT "Id", ROW_NUMBER() OVER (
                        PARTITION BY "PaymentRecordId" ORDER BY "CreatedOnUtc" DESC, "Id" DESC) AS rank
                    FROM payments.parent_payment_declarations WHERE "Status" = 'Declared'
                ), rejected AS (
                    UPDATE payments.parent_payment_declarations AS declaration
                    SET "Status" = 'Rejected', "ResolvedOnUtc" = CURRENT_TIMESTAMP
                    FROM ranked WHERE declaration."Id" = ranked."Id" AND ranked.rank > 1
                    RETURNING declaration.*
                ), events AS (
                    SELECT *, gen_random_uuid() AS event_id FROM rejected
                )
                INSERT INTO payments.outbox_messages
                    ("Id", "Module", "Type", "Payload", "OccurredOnUtc", "RetryCount")
                SELECT event_id, 'Payments', 'ParentPaymentDeclarationResolvedDomainEvent',
                    jsonb_build_object(
                        'eventId', event_id, 'occurredOnUtc', "ResolvedOnUtc",
                        'name', 'ParentPaymentDeclarationResolvedDomainEvent', 'sourceModule', 'Payments',
                        'payload', jsonb_build_object(
                            'declarationId', "Id", 'paymentRecordId', "PaymentRecordId",
                            'parentUserId', "ParentUserId", 'teacherUserId', "TeacherUserId",
                            'studentId', "StudentId", 'status', 3,
                            'resolvedOnUtc', "ResolvedOnUtc", 'eventId', event_id,
                            'occurredOnUtc', "ResolvedOnUtc")::text)::text,
                    "ResolvedOnUtc", 0
                FROM events;
                """);

            migrationBuilder.CreateIndex(
                name: "ux_payment_declaration_pending",
                schema: "payments",
                table: "parent_payment_declarations",
                column: "PaymentRecordId",
                unique: true,
                filter: "\"Status\" = 'Declared'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_payment_declaration_pending",
                schema: "payments",
                table: "parent_payment_declarations");

            // Tarihsel retleri geri açmayız; outbox geçmişi ve xmin korunur.
            migrationBuilder.CreateIndex(
                name: "IX_parent_payment_declarations_PaymentRecordId",
                schema: "payments",
                table: "parent_payment_declarations",
                column: "PaymentRecordId");
        }
    }
}
