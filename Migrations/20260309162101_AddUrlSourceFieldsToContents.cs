using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Web_Project.Migrations
{
    /// <inheritdoc />
    public partial class AddUrlSourceFieldsToContents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FetchError",
                table: "Contents",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "FetchStatus",
                table: "Contents",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Completed");

            migrationBuilder.AddColumn<string>(
                name: "SourceType",
                table: "Contents",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "FileUpload");

            migrationBuilder.AddColumn<string>(
                name: "SourceUrl",
                table: "Contents",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Contents_SourceType",
                table: "Contents",
                column: "SourceType");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Contents_SourceType",
                table: "Contents");

            migrationBuilder.DropColumn(
                name: "FetchError",
                table: "Contents");

            migrationBuilder.DropColumn(
                name: "FetchStatus",
                table: "Contents");

            migrationBuilder.DropColumn(
                name: "SourceType",
                table: "Contents");

            migrationBuilder.DropColumn(
                name: "SourceUrl",
                table: "Contents");
        }
    }
}
