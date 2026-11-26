using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Alhecasa.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddAgencyProfile : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "City",
            table: "Agencies",
            type: "nvarchar(100)",
            maxLength: 100,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Description",
            table: "Agencies",
            type: "nvarchar(1000)",
            maxLength: 1000,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "FacebookUrl",
            table: "Agencies",
            type: "nvarchar(200)",
            maxLength: 200,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "InstagramUrl",
            table: "Agencies",
            type: "nvarchar(200)",
            maxLength: 200,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "LogoContentType",
            table: "Agencies",
            type: "nvarchar(50)",
            maxLength: 50,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "LogoStorageKey",
            table: "Agencies",
            type: "nvarchar(64)",
            maxLength: 64,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "OpeningHours",
            table: "Agencies",
            type: "nvarchar(200)",
            maxLength: 200,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "PostalCode",
            table: "Agencies",
            type: "nvarchar(10)",
            maxLength: 10,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "State",
            table: "Agencies",
            type: "nvarchar(100)",
            maxLength: 100,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Street",
            table: "Agencies",
            type: "nvarchar(200)",
            maxLength: 200,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Website",
            table: "Agencies",
            type: "nvarchar(200)",
            maxLength: 200,
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "City",
            table: "Agencies");

        migrationBuilder.DropColumn(
            name: "Description",
            table: "Agencies");

        migrationBuilder.DropColumn(
            name: "FacebookUrl",
            table: "Agencies");

        migrationBuilder.DropColumn(
            name: "InstagramUrl",
            table: "Agencies");

        migrationBuilder.DropColumn(
            name: "LogoContentType",
            table: "Agencies");

        migrationBuilder.DropColumn(
            name: "LogoStorageKey",
            table: "Agencies");

        migrationBuilder.DropColumn(
            name: "OpeningHours",
            table: "Agencies");

        migrationBuilder.DropColumn(
            name: "PostalCode",
            table: "Agencies");

        migrationBuilder.DropColumn(
            name: "State",
            table: "Agencies");

        migrationBuilder.DropColumn(
            name: "Street",
            table: "Agencies");

        migrationBuilder.DropColumn(
            name: "Website",
            table: "Agencies");
    }
}
