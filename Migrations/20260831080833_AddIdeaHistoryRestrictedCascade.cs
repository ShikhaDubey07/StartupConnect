using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StartupConnect.Migrations
{
    /// <inheritdoc />
    public partial class AddIdeaHistoryRestrictedCascade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HasBeenEditedAfterApproval",
                table: "Ideas");

            migrationBuilder.DropColumn(
                name: "OriginalDescription",
                table: "Ideas");

            migrationBuilder.DropColumn(
                name: "OriginalTitle",
                table: "Ideas");

            migrationBuilder.CreateTable(
                name: "IdeaHistories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdeaId = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Tagline = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProblemStatement = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Solution = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TargetMarket = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BusinessModel = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CategoryId = table.Column<int>(type: "int", nullable: false),
                    MinimumFundRequired = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ExpectedTeamSize = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdeaHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IdeaHistories_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IdeaHistories_Ideas_IdeaId",
                        column: x => x.IdeaId,
                        principalTable: "Ideas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IdeaHistories_CategoryId",
                table: "IdeaHistories",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_IdeaHistories_IdeaId",
                table: "IdeaHistories",
                column: "IdeaId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IdeaHistories");

            migrationBuilder.AddColumn<bool>(
                name: "HasBeenEditedAfterApproval",
                table: "Ideas",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "OriginalDescription",
                table: "Ideas",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OriginalTitle",
                table: "Ideas",
                type: "nvarchar(max)",
                nullable: true);
        }
    }
}
