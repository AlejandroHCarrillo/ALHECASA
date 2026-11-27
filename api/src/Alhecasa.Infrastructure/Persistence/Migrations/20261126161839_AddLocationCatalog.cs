using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Alhecasa.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddLocationCatalog : Migration
{
    private static readonly string[] NeighborhoodKey = ["CityId", "Slug"];

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "CityId",
            table: "Properties",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "NeighborhoodId",
            table: "Properties",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "StateId",
            table: "Properties",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "GeoStates",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                Slug = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_GeoStates", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "GeoCities",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                StateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                Slug = table.Column<string>(type: "nvarchar(210)", maxLength: 210, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_GeoCities", x => x.Id);
                table.ForeignKey(
                    name: "FK_GeoCities_GeoStates_StateId",
                    column: x => x.StateId,
                    principalTable: "GeoStates",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "GeoNeighborhoods",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                Slug = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_GeoNeighborhoods", x => x.Id);
                table.ForeignKey(
                    name: "FK_GeoNeighborhoods_GeoCities_CityId",
                    column: x => x.CityId,
                    principalTable: "GeoCities",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Properties_CityId",
            table: "Properties",
            column: "CityId");

        migrationBuilder.CreateIndex(
            name: "IX_Properties_NeighborhoodId",
            table: "Properties",
            column: "NeighborhoodId");

        migrationBuilder.CreateIndex(
            name: "IX_Properties_StateId",
            table: "Properties",
            column: "StateId");

        migrationBuilder.CreateIndex(
            name: "IX_GeoCities_Slug",
            table: "GeoCities",
            column: "Slug",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_GeoCities_StateId",
            table: "GeoCities",
            column: "StateId");

        migrationBuilder.CreateIndex(
            name: "IX_GeoNeighborhoods_CityId_Slug",
            table: "GeoNeighborhoods",
            columns: NeighborhoodKey,
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_GeoStates_Slug",
            table: "GeoStates",
            column: "Slug",
            unique: true);

        migrationBuilder.AddForeignKey(
            name: "FK_Properties_GeoCities_CityId",
            table: "Properties",
            column: "CityId",
            principalTable: "GeoCities",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_Properties_GeoNeighborhoods_NeighborhoodId",
            table: "Properties",
            column: "NeighborhoodId",
            principalTable: "GeoNeighborhoods",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_Properties_GeoStates_StateId",
            table: "Properties",
            column: "StateId",
            principalTable: "GeoStates",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_Properties_GeoCities_CityId",
            table: "Properties");

        migrationBuilder.DropForeignKey(
            name: "FK_Properties_GeoNeighborhoods_NeighborhoodId",
            table: "Properties");

        migrationBuilder.DropForeignKey(
            name: "FK_Properties_GeoStates_StateId",
            table: "Properties");

        migrationBuilder.DropTable(
            name: "GeoNeighborhoods");

        migrationBuilder.DropTable(
            name: "GeoCities");

        migrationBuilder.DropTable(
            name: "GeoStates");

        migrationBuilder.DropIndex(
            name: "IX_Properties_CityId",
            table: "Properties");

        migrationBuilder.DropIndex(
            name: "IX_Properties_NeighborhoodId",
            table: "Properties");

        migrationBuilder.DropIndex(
            name: "IX_Properties_StateId",
            table: "Properties");

        migrationBuilder.DropColumn(
            name: "CityId",
            table: "Properties");

        migrationBuilder.DropColumn(
            name: "NeighborhoodId",
            table: "Properties");

        migrationBuilder.DropColumn(
            name: "StateId",
            table: "Properties");
    }
}
