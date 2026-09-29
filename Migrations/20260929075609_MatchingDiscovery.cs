using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StartupConnect.Migrations
{
    /// <inheritdoc />
    public partial class MatchingDiscovery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_IdeaViews_IdeaId",
                table: "IdeaViews");

            migrationBuilder.AddColumn<DateTime>(
                name: "RespondedAt",
                table: "Interests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResponseNote",
                table: "Interests",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ViewerKey",
                table: "IdeaViews",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            // Existing rows: members keyed by user id, anonymous rows keep a unique legacy key (no visitor id was stored).
            migrationBuilder.Sql(@"UPDATE IdeaViews SET ViewerKey = CASE WHEN UserId IS NOT NULL THEN CONCAT('u:', UserId) ELSE CONCAT('legacy:', Id) END WHERE ViewerKey IS NULL;");
            // Remove refresh-inflated duplicates: keep the first view per viewer, idea and day.
            migrationBuilder.Sql(@"WITH d AS (SELECT Id, ROW_NUMBER() OVER (PARTITION BY IdeaId, ViewerKey, CAST(CreatedAt AS date) ORDER BY CreatedAt, Id) AS rn FROM IdeaViews)
DELETE FROM d WHERE rn > 1;");

            migrationBuilder.CreateIndex(
                name: "IX_IdeaViews_IdeaId_ViewerKey_CreatedAt",
                table: "IdeaViews",
                columns: new[] { "IdeaId", "ViewerKey", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Ideas_Status_CategoryId",
                table: "Ideas",
                columns: new[] { "Status", "CategoryId" });

            migrationBuilder.CreateIndex(
                name: "IX_Ideas_Status_PublishedAt",
                table: "Ideas",
                columns: new[] { "Status", "PublishedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_IdeaViews_IdeaId_ViewerKey_CreatedAt",
                table: "IdeaViews");

            migrationBuilder.DropIndex(
                name: "IX_Ideas_Status_CategoryId",
                table: "Ideas");

            migrationBuilder.DropIndex(
                name: "IX_Ideas_Status_PublishedAt",
                table: "Ideas");

            migrationBuilder.DropColumn(
                name: "RespondedAt",
                table: "Interests");

            migrationBuilder.DropColumn(
                name: "ResponseNote",
                table: "Interests");

            migrationBuilder.DropColumn(
                name: "ViewerKey",
                table: "IdeaViews");

            migrationBuilder.CreateIndex(
                name: "IX_IdeaViews_IdeaId",
                table: "IdeaViews",
                column: "IdeaId");
        }
    }
}
