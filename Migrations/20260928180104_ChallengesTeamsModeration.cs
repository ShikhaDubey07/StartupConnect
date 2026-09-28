using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StartupConnect.Migrations
{
    /// <inheritdoc />
    public partial class ChallengesTeamsModeration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ---- Data preparation: make existing rows fit the new lengths / unique indexes ----
            migrationBuilder.Sql("UPDATE TeamMembers SET Role = LEFT(Role, 60) WHERE LEN(Role) > 60;");
            migrationBuilder.Sql("UPDATE StartupChallenges SET Title = LEFT(Title, 150) WHERE LEN(Title) > 150;");
            migrationBuilder.Sql("UPDATE StartupChallenges SET Prize = LEFT(Prize, 150) WHERE LEN(Prize) > 150;");
            migrationBuilder.Sql("UPDATE IdeaMilestones SET Title = LEFT(Title, 150) WHERE LEN(Title) > 150;");
            migrationBuilder.Sql("UPDATE IdeaMilestones SET Description = LEFT(Description, 1000) WHERE LEN(Description) > 1000;");
            migrationBuilder.Sql(@"WITH d AS (SELECT Id, ROW_NUMBER() OVER (PARTITION BY TeamId, UserId ORDER BY Id) AS rn FROM TeamMembers)
DELETE FROM d WHERE rn > 1;");
            migrationBuilder.Sql(@"WITH d AS (SELECT Id, ROW_NUMBER() OVER (PARTITION BY ChallengeId, IdeaId ORDER BY Id) AS rn FROM ChallengeSubmissions)
DELETE FROM d WHERE rn > 1;");

            migrationBuilder.DropIndex(
                name: "IX_UserActivities_UserId",
                table: "UserActivities");

            migrationBuilder.DropIndex(
                name: "IX_TeamMessages_TeamId",
                table: "TeamMessages");

            migrationBuilder.DropIndex(
                name: "IX_TeamMembers_TeamId",
                table: "TeamMembers");

            migrationBuilder.DropIndex(
                name: "IX_ChallengeSubmissions_ChallengeId",
                table: "ChallengeSubmissions");

            migrationBuilder.AlterColumn<string>(
                name: "Role",
                table: "TeamMembers",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "StartupChallenges",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Prize",
                table: "StartupChallenges",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<int>(
                name: "CategoryId",
                table: "StartupChallenges",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CoverImageUrl",
                table: "StartupChallenges",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Eligibility",
                table: "StartupChallenges",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ResultsAnnouncedAt",
                table: "StartupChallenges",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Rules",
                table: "StartupChallenges",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResolutionNote",
                table: "IdeaReports",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ResolvedAt",
                table: "IdeaReports",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResolvedByUserId",
                table: "IdeaReports",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "IdeaReports",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "IdeaMilestones",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "IdeaMilestones",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<string>(
                name: "AssigneeUserId",
                table: "IdeaMilestones",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CompletedAt",
                table: "IdeaMilestones",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedByUserId",
                table: "IdeaMilestones",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AwardTitle",
                table: "ChallengeSubmissions",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsShortlisted",
                table: "ChallengeSubmissions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsWinner",
                table: "ChallengeSubmissions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "JudgeFeedback",
                table: "ChallengeSubmissions",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "FounderVerificationRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    LinkedInUrl = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReviewedByUserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FounderVerificationRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FounderVerificationRequests_AspNetUsers_ReviewedByUserId",
                        column: x => x.ReviewedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FounderVerificationRequests_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserActivities_UserId_CreatedAt",
                table: "UserActivities",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TeamMessages_TeamId_Id",
                table: "TeamMessages",
                columns: new[] { "TeamId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_TeamMembers_TeamId_UserId",
                table: "TeamMembers",
                columns: new[] { "TeamId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StartupChallenges_CategoryId",
                table: "StartupChallenges",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_IdeaReports_ResolvedByUserId",
                table: "IdeaReports",
                column: "ResolvedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_IdeaReports_Status_IdeaId",
                table: "IdeaReports",
                columns: new[] { "Status", "IdeaId" });

            migrationBuilder.CreateIndex(
                name: "IX_IdeaMilestones_AssigneeUserId",
                table: "IdeaMilestones",
                column: "AssigneeUserId");

            migrationBuilder.CreateIndex(
                name: "IX_IdeaMilestones_CreatedByUserId",
                table: "IdeaMilestones",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ChallengeSubmissions_ChallengeId_IdeaId",
                table: "ChallengeSubmissions",
                columns: new[] { "ChallengeId", "IdeaId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FounderVerificationRequests_ReviewedByUserId",
                table: "FounderVerificationRequests",
                column: "ReviewedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FounderVerificationRequests_Status_CreatedAt",
                table: "FounderVerificationRequests",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_FounderVerificationRequests_UserId",
                table: "FounderVerificationRequests",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_IdeaMilestones_AspNetUsers_AssigneeUserId",
                table: "IdeaMilestones",
                column: "AssigneeUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_IdeaMilestones_AspNetUsers_CreatedByUserId",
                table: "IdeaMilestones",
                column: "CreatedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_IdeaReports_AspNetUsers_ResolvedByUserId",
                table: "IdeaReports",
                column: "ResolvedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_StartupChallenges_Categories_CategoryId",
                table: "StartupChallenges",
                column: "CategoryId",
                principalTable: "Categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            // ---- Data fix: every team's founder (idea owner) is a team member with the Founder role ----
            migrationBuilder.Sql(@"UPDATE tm SET Role = 'Founder'
FROM TeamMembers tm
JOIN Teams t ON t.Id = tm.TeamId
JOIN Ideas i ON i.Id = t.IdeaId
WHERE tm.UserId = i.SubmitterUserId;");
            migrationBuilder.Sql(@"INSERT INTO TeamMembers (TeamId, UserId, Role, JoinedAt)
SELECT t.Id, i.SubmitterUserId, 'Founder', t.CreatedAt
FROM Teams t
JOIN Ideas i ON i.Id = t.IdeaId
WHERE NOT EXISTS (SELECT 1 FROM TeamMembers tm WHERE tm.TeamId = t.Id AND tm.UserId = i.SubmitterUserId);");
            migrationBuilder.Sql("UPDATE IdeaMilestones SET CompletedAt = CreatedAt WHERE IsCompleted = 1 AND CompletedAt IS NULL;");
            // Profiles flagged by the old one-click "request verification" become pending requests.
            migrationBuilder.Sql(@"INSERT INTO FounderVerificationRequests (UserId, Note, LinkedInUrl, Status, CreatedAt)
SELECT p.UserId, 'Requested before verification notes were collected.', LEFT(p.LinkedInUrl, 300), 0, SYSUTCDATETIME()
FROM UserProfiles p
WHERE p.VerificationRequested = 1 AND p.IsVerifiedFounder = 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_IdeaMilestones_AspNetUsers_AssigneeUserId",
                table: "IdeaMilestones");

            migrationBuilder.DropForeignKey(
                name: "FK_IdeaMilestones_AspNetUsers_CreatedByUserId",
                table: "IdeaMilestones");

            migrationBuilder.DropForeignKey(
                name: "FK_IdeaReports_AspNetUsers_ResolvedByUserId",
                table: "IdeaReports");

            migrationBuilder.DropForeignKey(
                name: "FK_StartupChallenges_Categories_CategoryId",
                table: "StartupChallenges");

            migrationBuilder.DropTable(
                name: "FounderVerificationRequests");

            migrationBuilder.DropIndex(
                name: "IX_UserActivities_UserId_CreatedAt",
                table: "UserActivities");

            migrationBuilder.DropIndex(
                name: "IX_TeamMessages_TeamId_Id",
                table: "TeamMessages");

            migrationBuilder.DropIndex(
                name: "IX_TeamMembers_TeamId_UserId",
                table: "TeamMembers");

            migrationBuilder.DropIndex(
                name: "IX_StartupChallenges_CategoryId",
                table: "StartupChallenges");

            migrationBuilder.DropIndex(
                name: "IX_IdeaReports_ResolvedByUserId",
                table: "IdeaReports");

            migrationBuilder.DropIndex(
                name: "IX_IdeaReports_Status_IdeaId",
                table: "IdeaReports");

            migrationBuilder.DropIndex(
                name: "IX_IdeaMilestones_AssigneeUserId",
                table: "IdeaMilestones");

            migrationBuilder.DropIndex(
                name: "IX_IdeaMilestones_CreatedByUserId",
                table: "IdeaMilestones");

            migrationBuilder.DropIndex(
                name: "IX_ChallengeSubmissions_ChallengeId_IdeaId",
                table: "ChallengeSubmissions");

            migrationBuilder.DropColumn(
                name: "CategoryId",
                table: "StartupChallenges");

            migrationBuilder.DropColumn(
                name: "CoverImageUrl",
                table: "StartupChallenges");

            migrationBuilder.DropColumn(
                name: "Eligibility",
                table: "StartupChallenges");

            migrationBuilder.DropColumn(
                name: "ResultsAnnouncedAt",
                table: "StartupChallenges");

            migrationBuilder.DropColumn(
                name: "Rules",
                table: "StartupChallenges");

            migrationBuilder.DropColumn(
                name: "ResolutionNote",
                table: "IdeaReports");

            migrationBuilder.DropColumn(
                name: "ResolvedAt",
                table: "IdeaReports");

            migrationBuilder.DropColumn(
                name: "ResolvedByUserId",
                table: "IdeaReports");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "IdeaReports");

            migrationBuilder.DropColumn(
                name: "AssigneeUserId",
                table: "IdeaMilestones");

            migrationBuilder.DropColumn(
                name: "CompletedAt",
                table: "IdeaMilestones");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "IdeaMilestones");

            migrationBuilder.DropColumn(
                name: "AwardTitle",
                table: "ChallengeSubmissions");

            migrationBuilder.DropColumn(
                name: "IsShortlisted",
                table: "ChallengeSubmissions");

            migrationBuilder.DropColumn(
                name: "IsWinner",
                table: "ChallengeSubmissions");

            migrationBuilder.DropColumn(
                name: "JudgeFeedback",
                table: "ChallengeSubmissions");

            migrationBuilder.AlterColumn<string>(
                name: "Role",
                table: "TeamMembers",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(60)",
                oldMaxLength: 60);

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "StartupChallenges",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(150)",
                oldMaxLength: 150);

            migrationBuilder.AlterColumn<string>(
                name: "Prize",
                table: "StartupChallenges",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(150)",
                oldMaxLength: 150);

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "IdeaMilestones",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(150)",
                oldMaxLength: 150);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "IdeaMilestones",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(1000)",
                oldMaxLength: 1000);

            migrationBuilder.CreateIndex(
                name: "IX_UserActivities_UserId",
                table: "UserActivities",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_TeamMessages_TeamId",
                table: "TeamMessages",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_TeamMembers_TeamId",
                table: "TeamMembers",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_ChallengeSubmissions_ChallengeId",
                table: "ChallengeSubmissions",
                column: "ChallengeId");
        }
    }
}
