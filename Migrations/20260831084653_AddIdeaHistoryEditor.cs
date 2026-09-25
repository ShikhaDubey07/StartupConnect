using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StartupConnect.Migrations
{
    /// <inheritdoc />
    public partial class AddIdeaHistoryEditor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EditorId",
                table: "IdeaHistories",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_IdeaHistories_EditorId",
                table: "IdeaHistories",
                column: "EditorId");

            migrationBuilder.AddForeignKey(
                name: "FK_IdeaHistories_AspNetUsers_EditorId",
                table: "IdeaHistories",
                column: "EditorId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_IdeaHistories_AspNetUsers_EditorId",
                table: "IdeaHistories");

            migrationBuilder.DropIndex(
                name: "IX_IdeaHistories_EditorId",
                table: "IdeaHistories");

            migrationBuilder.DropColumn(
                name: "EditorId",
                table: "IdeaHistories");
        }
    }
}
