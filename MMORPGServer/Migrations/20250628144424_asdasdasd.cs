using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MMORPGServer.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class asdasdasd : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Players",
                type: "nvarchar(15)",
                maxLength: 15,
                nullable: false,
                collation: "LATIN1_GENERAL_100_CI_AS_SC_UTF8",
                oldClrType: typeof(string),
                oldType: "nvarchar(15)",
                oldMaxLength: 15,
                oldCollation: "Arabic_CI_AS");

            migrationBuilder.AlterColumn<DateTime>(
                name: "LastModifiedAt",
                table: "Players",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "GETUTCDATE()",
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Players",
                type: "nvarchar(15)",
                maxLength: 15,
                nullable: false,
                collation: "Arabic_CI_AS",
                oldClrType: typeof(string),
                oldType: "nvarchar(15)",
                oldMaxLength: 15,
                oldCollation: "LATIN1_GENERAL_100_CI_AS_SC_UTF8");

            migrationBuilder.AlterColumn<DateTime>(
                name: "LastModifiedAt",
                table: "Players",
                type: "datetime2",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldDefaultValueSql: "GETUTCDATE()");
        }
    }
}
