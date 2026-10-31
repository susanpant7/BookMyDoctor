using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BookMyDoctor.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class KnowledgeDocumentTitles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DocumentTitle",
                table: "KnowledgeChunks",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "Position",
                table: "KnowledgeChunks",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DocumentTitle",
                table: "KnowledgeChunks");

            migrationBuilder.DropColumn(
                name: "Position",
                table: "KnowledgeChunks");
        }
    }
}
