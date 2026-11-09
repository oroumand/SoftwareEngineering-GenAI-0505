using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlogSpecLab.Infra.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class InitialBlogSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserAccess",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    CanWrite = table.Column<bool>(type: "INTEGER", nullable: false),
                    CanReview = table.Column<bool>(type: "INTEGER", nullable: false),
                    CanPublish = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserAccess", x => x.UserId);
                });

            migrationBuilder.CreateTable(
                name: "Credentials",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    TokenHash = table.Column<string>(type: "TEXT", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    RevokedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Credentials", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Credentials_UserAccess_UserId",
                        column: x => x.UserId,
                        principalTable: "UserAccess",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Posts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    OwnerId = table.Column<string>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    Body = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    ContentRevision = table.Column<int>(type: "INTEGER", nullable: false),
                    Version = table.Column<Guid>(type: "TEXT", nullable: false),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    CurrentReviewId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Publication_PublisherId = table.Column<string>(type: "TEXT", nullable: true),
                    Publication_PublishedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Publication_ApprovedReviewId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Posts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Posts_UserAccess_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "UserAccess",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Posts_UserAccess_Publication_PublisherId",
                        column: x => x.Publication_PublisherId,
                        principalTable: "UserAccess",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReviewDecisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PostId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ContentRevision = table.Column<int>(type: "INTEGER", nullable: false),
                    ReviewedTitle = table.Column<string>(type: "TEXT", nullable: false),
                    ReviewedBody = table.Column<string>(type: "TEXT", nullable: false),
                    Decision = table.Column<string>(type: "TEXT", nullable: false),
                    ReviewerId = table.Column<string>(type: "TEXT", nullable: false),
                    DecidedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Reason = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReviewDecisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReviewDecisions_Posts_PostId",
                        column: x => x.PostId,
                        principalTable: "Posts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReviewDecisions_UserAccess_ReviewerId",
                        column: x => x.ReviewerId,
                        principalTable: "UserAccess",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Credentials_TokenHash",
                table: "Credentials",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Credentials_UserId",
                table: "Credentials",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Posts_CurrentReviewId",
                table: "Posts",
                column: "CurrentReviewId");

            migrationBuilder.CreateIndex(
                name: "IX_Posts_OwnerId_IsDeleted",
                table: "Posts",
                columns: new[] { "OwnerId", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_Posts_Publication_ApprovedReviewId",
                table: "Posts",
                column: "Publication_ApprovedReviewId");

            migrationBuilder.CreateIndex(
                name: "IX_Posts_Publication_PublisherId",
                table: "Posts",
                column: "Publication_PublisherId");

            migrationBuilder.CreateIndex(
                name: "IX_Posts_Status_IsDeleted",
                table: "Posts",
                columns: new[] { "Status", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_ReviewDecisions_PostId",
                table: "ReviewDecisions",
                column: "PostId");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewDecisions_ReviewerId",
                table: "ReviewDecisions",
                column: "ReviewerId");

            migrationBuilder.AddForeignKey(
                name: "FK_Posts_ReviewDecisions_CurrentReviewId",
                table: "Posts",
                column: "CurrentReviewId",
                principalTable: "ReviewDecisions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Posts_ReviewDecisions_Publication_ApprovedReviewId",
                table: "Posts",
                column: "Publication_ApprovedReviewId",
                principalTable: "ReviewDecisions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Posts_UserAccess_OwnerId",
                table: "Posts");

            migrationBuilder.DropForeignKey(
                name: "FK_Posts_UserAccess_Publication_PublisherId",
                table: "Posts");

            migrationBuilder.DropForeignKey(
                name: "FK_ReviewDecisions_UserAccess_ReviewerId",
                table: "ReviewDecisions");

            migrationBuilder.DropForeignKey(
                name: "FK_Posts_ReviewDecisions_CurrentReviewId",
                table: "Posts");

            migrationBuilder.DropForeignKey(
                name: "FK_Posts_ReviewDecisions_Publication_ApprovedReviewId",
                table: "Posts");

            migrationBuilder.DropTable(
                name: "Credentials");

            migrationBuilder.DropTable(
                name: "UserAccess");

            migrationBuilder.DropTable(
                name: "ReviewDecisions");

            migrationBuilder.DropTable(
                name: "Posts");
        }
    }
}
