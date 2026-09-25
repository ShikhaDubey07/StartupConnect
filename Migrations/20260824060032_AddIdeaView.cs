using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StartupConnect.Migrations
{
    /// <inheritdoc />
    public partial class AddIdeaView : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_IdeaReports_AspNetUsers_UserId",
                table: "IdeaReports");

            migrationBuilder.CreateTable(
                name: "IdeaViews",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdeaId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdeaViews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IdeaViews_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_IdeaViews_Ideas_IdeaId",
                        column: x => x.IdeaId,
                        principalTable: "Ideas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IdeaViews_IdeaId",
                table: "IdeaViews",
                column: "IdeaId");

            migrationBuilder.CreateIndex(
                name: "IX_IdeaViews_UserId",
                table: "IdeaViews",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_IdeaReports_AspNetUsers_UserId",
                table: "IdeaReports",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_IdeaReports_AspNetUsers_UserId",
                table: "IdeaReports");

            migrationBuilder.DropTable(
                name: "IdeaViews");

            migrationBuilder.AddForeignKey(
                name: "FK_IdeaReports_AspNetUsers_UserId",
                table: "IdeaReports",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
