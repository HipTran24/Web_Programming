using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Web_Project.Migrations
{
    /// <inheritdoc />
    public partial class MakeUrlFieldsNullableForFileUpload : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "SourceUrl",
                table: "Contents",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(2048)",
                oldMaxLength: 2048);

            migrationBuilder.AlterColumn<string>(
                name: "FetchStatus",
                table: "Contents",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(32)",
                oldMaxLength: 32);

            migrationBuilder.AlterColumn<string>(
                name: "FetchError",
                table: "Contents",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(2000)",
                oldMaxLength: 2000);

            migrationBuilder.Sql(
                """
                UPDATE [Contents]
                SET [SourceUrl] = NULL,
                    [FetchStatus] = NULL,
                    [FetchError] = NULL
                WHERE [SourceType] = N'FileUpload';
                """);

            migrationBuilder.Sql(
                """
                UPDATE [Contents]
                SET [SourceUrl] = [FilePath]
                WHERE [SourceType] <> N'FileUpload'
                  AND [SourceUrl] IS NULL
                  AND [FilePath] IS NOT NULL;
                """);

            migrationBuilder.Sql(
                """
                UPDATE [Contents]
                SET [FetchStatus] = N'PendingProcessing'
                WHERE [SourceType] <> N'FileUpload'
                  AND [FetchStatus] IS NULL;
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Contents_SourceType",
                table: "Contents",
                sql: "[SourceType] IN (N'FileUpload', N'TextUrl', N'VideoUrl', N'DocumentUrl')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Contents_UrlFieldsBySource",
                table: "Contents",
                sql: "(([SourceType] = N'FileUpload' AND [SourceUrl] IS NULL AND [FetchStatus] IS NULL AND [FetchError] IS NULL) OR ([SourceType] <> N'FileUpload' AND [SourceUrl] IS NOT NULL AND [FetchStatus] IS NOT NULL))");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Contents_SourceType",
                table: "Contents");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Contents_UrlFieldsBySource",
                table: "Contents");

            migrationBuilder.AlterColumn<string>(
                name: "SourceUrl",
                table: "Contents",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(2048)",
                oldMaxLength: 2048,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "FetchStatus",
                table: "Contents",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(32)",
                oldMaxLength: 32,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "FetchError",
                table: "Contents",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(2000)",
                oldMaxLength: 2000,
                oldNullable: true);
        }
    }
}
