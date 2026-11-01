using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyHospitalAI.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AiModelApiKeyName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ApiKeyName",
                table: "AiModels",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApiKeyName",
                table: "AiModels");
        }
    }
}
