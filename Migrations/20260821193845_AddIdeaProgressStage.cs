using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StartupConnect.Migrations
{
    /// <inheritdoc />
    public partial class AddIdeaProgressStage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ProgressStage",
                table: "Ideas",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProgressStage",
                table: "Ideas");
        }
    }
}
