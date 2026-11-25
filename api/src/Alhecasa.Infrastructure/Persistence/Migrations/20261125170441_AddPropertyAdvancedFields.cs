using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Alhecasa.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddPropertyAdvancedFields : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "Amenities",
            table: "Properties",
            type: "int",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<bool>(
            name: "Furnished",
            table: "Properties",
            type: "bit",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<int>(
            name: "ParkingSpaces",
            table: "Properties",
            type: "int",
            nullable: true);

        migrationBuilder.AddColumn<bool>(
            name: "PetsAllowed",
            table: "Properties",
            type: "bit",
            nullable: false,
            defaultValue: false);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "Amenities",
            table: "Properties");

        migrationBuilder.DropColumn(
            name: "Furnished",
            table: "Properties");

        migrationBuilder.DropColumn(
            name: "ParkingSpaces",
            table: "Properties");

        migrationBuilder.DropColumn(
            name: "PetsAllowed",
            table: "Properties");
    }
}
