using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Alhecasa.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddAgencyKind : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "Kind",
            table: "Agencies",
            type: "nvarchar(20)",
            maxLength: 20,
            nullable: false,
            defaultValue: "Company");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "Kind",
            table: "Agencies");
    }
}
