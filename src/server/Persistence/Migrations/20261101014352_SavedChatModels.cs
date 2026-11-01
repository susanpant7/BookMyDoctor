using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyHospitalAI.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SavedChatModels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ChatModels",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false, collation: "NOCASE"),
                    BaseUrl = table.Column<string>(type: "TEXT", nullable: false),
                    Model = table.Column<string>(type: "TEXT", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatModels", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChatModels_Name",
                table: "ChatModels",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChatModels");
        }
    }
}
