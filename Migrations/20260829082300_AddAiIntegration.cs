using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StartupConnect.Migrations
{
    /// <inheritdoc />
    public partial class AddAiIntegration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "InvestorPitchSummary",
                table: "IdeaAnalyses",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CoFounderMatchInsights",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId1 = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    UserId2 = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Rationale = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CoFounderMatchInsights", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CoFounderMatchInsights_UserId1_UserId2",
                table: "CoFounderMatchInsights",
                columns: new[] { "UserId1", "UserId2" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CoFounderMatchInsights");

            migrationBuilder.DropColumn(
                name: "InvestorPitchSummary",
                table: "IdeaAnalyses");
        }
    }
}
