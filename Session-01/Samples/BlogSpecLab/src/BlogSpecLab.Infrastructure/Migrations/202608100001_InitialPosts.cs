using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using BlogSpecLab.Infrastructure.Posts.Persistence;

#nullable disable

namespace BlogSpecLab.Infrastructure.Migrations;

[DbContext(typeof(BlogSpecLabDbContext))]
[Migration("202608100001_InitialPosts")]
public partial class InitialPosts : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "posts",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                author_id = table.Column<string>(type: "text", nullable: false),
                title = table.Column<string>(type: "text", nullable: false),
                content = table.Column<string>(type: "text", nullable: false),
                status = table.Column<string>(type: "text", nullable: false),
                published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                version_token = table.Column<string>(type: "text", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_posts", item => item.id);
                table.CheckConstraint("CK_posts_status_published_at", "(status = 'Draft' AND published_at IS NULL) OR (status = 'Published' AND published_at IS NOT NULL)");
            });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "posts");
    }
}
