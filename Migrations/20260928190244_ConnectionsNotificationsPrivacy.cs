using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StartupConnect.Migrations
{
    /// <inheritdoc />
    public partial class ConnectionsNotificationsPrivacy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Notifications_UserId",
                table: "Notifications");

            migrationBuilder.AddColumn<int>(
                name: "Category",
                table: "Notifications",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // Backfill categories of existing notifications from their titles (used for icons/preferences).
            migrationBuilder.Sql(@"
UPDATE Notifications SET Category = CASE
    WHEN Title LIKE '%Like%' THEN 3
    WHEN Title LIKE '%Comment%' THEN 2
    WHEN Title LIKE '%Match%' THEN 9
    WHEN Title LIKE '%Connection%' THEN 4
    WHEN Title LIKE '%Interest%' OR Title LIKE '%Request%' THEN 1
    WHEN Title LIKE '%milestone%' OR Title LIKE '%team%' THEN 6
    WHEN Title LIKE '%challenge%' OR Title LIKE '%result%' THEN 7
    WHEN Title LIKE '%Approved%' OR Title LIKE '%Revision%' OR Title LIKE '%verif%' OR Title LIKE '%report%' OR Title LIKE '%unpublished%' THEN 8
    ELSE 0 END;");

            migrationBuilder.CreateTable(
                name: "Connections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RequesterId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    AddresseeId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    UserAId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    UserBId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Message = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RespondedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Connections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Connections_AspNetUsers_AddresseeId",
                        column: x => x.AddresseeId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Connections_AspNetUsers_RequesterId",
                        column: x => x.RequesterId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId_IsRead_CreatedAt",
                table: "Notifications",
                columns: new[] { "UserId", "IsRead", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Connections_AddresseeId_Status",
                table: "Connections",
                columns: new[] { "AddresseeId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Connections_RequesterId_Status",
                table: "Connections",
                columns: new[] { "RequesterId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Connections_UserAId_UserBId",
                table: "Connections",
                columns: new[] { "UserAId", "UserBId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Connections");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_UserId_IsRead_CreatedAt",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "Notifications");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId",
                table: "Notifications",
                column: "UserId");
        }
    }
}
