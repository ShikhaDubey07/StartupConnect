using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StartupConnect.Migrations
{
    /// <inheritdoc />
    public partial class AddIdeaAnalysis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IdeaAnalyses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdeaId = table.Column<int>(type: "int", nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProblemsAndSolutions = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TargetUsers = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MarketPotential = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Risks = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RevenueModels = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TeamSuggestions = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OverallScore = table.Column<int>(type: "int", nullable: false),
                    SimilarIdeasJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdeaAnalyses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IdeaAnalyses_Ideas_IdeaId",
                        column: x => x.IdeaId,
                        principalTable: "Ideas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IdeaAnalyses_IdeaId",
                table: "IdeaAnalyses",
                column: "IdeaId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IdeaAnalyses");
        }
    }
}
