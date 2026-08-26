using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VirtoCommerce.SalesRep.Data.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class SalesRep_Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SalesRepDocumentMetadata",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    FileId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    IsPinned = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    Summary = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    PageCount = table.Column<int>(type: "int", nullable: true),
                    PreviewUrl = table.Column<string>(type: "nvarchar(2083)", maxLength: 2083, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesRepDocumentMetadata", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SalesRepDocumentMetadata_FileId",
                table: "SalesRepDocumentMetadata",
                column: "FileId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalesRepDocumentMetadata_IsPinned_CreatedDate",
                table: "SalesRepDocumentMetadata",
                columns: new[] { "IsPinned", "CreatedDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SalesRepDocumentMetadata");
        }
    }
}
