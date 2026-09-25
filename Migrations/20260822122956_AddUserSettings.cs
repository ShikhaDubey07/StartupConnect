using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StartupConnect.Migrations
{
    /// <inheritdoc />
    public partial class AddUserSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    EmailNotifications = table.Column<bool>(type: "bit", nullable: false),
                    InAppNotifications = table.Column<bool>(type: "bit", nullable: false),
                    NotifyOnMatches = table.Column<bool>(type: "bit", nullable: false),
                    NotifyOnMessages = table.Column<bool>(type: "bit", nullable: false),
                    ProfileVisibility = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ShowEmail = table.Column<bool>(type: "bit", nullable: false),
                    ShowLocation = table.Column<bool>(type: "bit", nullable: false),
                    ShowAge = table.Column<bool>(type: "bit", nullable: false),
                    ThemePreference = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserSettings_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserSettings_UserId",
                table: "UserSettings",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserSettings");
        }
    }
}
