using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

// El índice compuesto lo genera EF con un arreglo literal; una migración se ejecuta una sola vez, así que CA1861 no aplica.
#pragma warning disable CA1861

namespace Alhecasa.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddInquiries : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Inquiries",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                PropertyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                AgencyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                PropertyTitle = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                Phone = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                Message = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Inquiries", x => x.Id);
                table.ForeignKey(
                    name: "FK_Inquiries_Agencies_AgencyId",
                    column: x => x.AgencyId,
                    principalTable: "Agencies",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_Inquiries_Properties_PropertyId",
                    column: x => x.PropertyId,
                    principalTable: "Properties",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "InquiryFollowUps",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                InquiryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                AgencyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Kind = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                Text = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                AuthorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                AuthorName = table.Column<string>(type: "nvarchar(201)", maxLength: 201, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_InquiryFollowUps", x => x.Id);
                table.ForeignKey(
                    name: "FK_InquiryFollowUps_Inquiries_InquiryId",
                    column: x => x.InquiryId,
                    principalTable: "Inquiries",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Inquiries_AgencyId_Status_CreatedAt",
            table: "Inquiries",
            columns: new[] { "AgencyId", "Status", "CreatedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_Inquiries_PropertyId",
            table: "Inquiries",
            column: "PropertyId");

        migrationBuilder.CreateIndex(
            name: "IX_InquiryFollowUps_AgencyId",
            table: "InquiryFollowUps",
            column: "AgencyId");

        migrationBuilder.CreateIndex(
            name: "IX_InquiryFollowUps_InquiryId",
            table: "InquiryFollowUps",
            column: "InquiryId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "InquiryFollowUps");

        migrationBuilder.DropTable(
            name: "Inquiries");
    }
}
