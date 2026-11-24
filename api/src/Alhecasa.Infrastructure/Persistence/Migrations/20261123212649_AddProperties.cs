using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Alhecasa.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddProperties : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Properties",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                AgencyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Title = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                Type = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                Operation = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                Price = table.Column<decimal>(type: "decimal(15,2)", precision: 15, scale: 2, nullable: false),
                Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                Street = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                Neighborhood = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                City = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                State = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                PostalCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                Bedrooms = table.Column<int>(type: "int", nullable: true),
                Bathrooms = table.Column<int>(type: "int", nullable: true),
                ConstructionArea = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                LandArea = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Properties", x => x.Id);
                table.ForeignKey(
                    name: "FK_Properties_Agencies_AgencyId",
                    column: x => x.AgencyId,
                    principalTable: "Agencies",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Properties_AgencyId",
            table: "Properties",
            column: "AgencyId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "Properties");
    }
}
