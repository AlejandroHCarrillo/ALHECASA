using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Alhecasa.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddPlansAndFeatured : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "FeaturedUntil",
            table: "Properties",
            type: "datetimeoffset",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "Plans",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                Kind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                Audience = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                Limit = table.Column<int>(type: "int", nullable: true),
                Period = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                Price = table.Column<decimal>(type: "decimal(15,2)", precision: 15, scale: 2, nullable: false),
                Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                IsActive = table.Column<bool>(type: "bit", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Plans", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Subscriptions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                AgencyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                PlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                PlanName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                Kind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                Limit = table.Column<int>(type: "int", nullable: true),
                Period = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                AmountPaid = table.Column<decimal>(type: "decimal(15,2)", precision: 15, scale: 2, nullable: false),
                Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                PaymentReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                StartsAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                EndsAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                CancelledAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Subscriptions", x => x.Id);
                table.ForeignKey(
                    name: "FK_Subscriptions_Agencies_AgencyId",
                    column: x => x.AgencyId,
                    principalTable: "Agencies",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_Subscriptions_Plans_PlanId",
                    column: x => x.PlanId,
                    principalTable: "Plans",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Subscriptions_AgencyId_Kind",
            table: "Subscriptions",
            columns: ["AgencyId", "Kind"]);

        migrationBuilder.CreateIndex(
            name: "IX_Subscriptions_PlanId",
            table: "Subscriptions",
            column: "PlanId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "Subscriptions");

        migrationBuilder.DropTable(
            name: "Plans");

        migrationBuilder.DropColumn(
            name: "FeaturedUntil",
            table: "Properties");
    }
}
