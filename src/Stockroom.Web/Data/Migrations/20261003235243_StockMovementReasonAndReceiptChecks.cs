using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Stockroom.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class StockMovementReasonAndReceiptChecks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "CK_StockMovements_IssueReason",
                table: "StockMovements",
                sql: "\"Action\" <> 'Issue' OR \"Reason\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_StockMovements_Reason",
                table: "StockMovements",
                sql: "\"Reason\" IS NULL OR length(trim(\"Reason\")) BETWEEN 1 AND 500");

            migrationBuilder.AddCheckConstraint(
                name: "CK_StockMovements_ReceiptRequest",
                table: "StockMovements",
                sql: "\"Action\" <> 'Receipt' OR \"PurchaseRequestId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_StockMovements_IssueReason",
                table: "StockMovements");

            migrationBuilder.DropCheckConstraint(
                name: "CK_StockMovements_Reason",
                table: "StockMovements");

            migrationBuilder.DropCheckConstraint(
                name: "CK_StockMovements_ReceiptRequest",
                table: "StockMovements");
        }
    }
}
