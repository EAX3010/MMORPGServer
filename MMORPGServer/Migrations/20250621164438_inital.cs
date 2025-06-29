using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MMORPGServer.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class inital : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Players",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false, collation: "Arabic_CI_AS"),
                    Level = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    Experience = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                    MapId = table.Column<short>(type: "smallint", nullable: false),
                    X = table.Column<short>(type: "smallint", nullable: false),
                    Y = table.Column<short>(type: "smallint", nullable: false),
                    Gold = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                    ConquerPoints = table.Column<int>(type: "int", nullable: false),
                    BoundConquerPoints = table.Column<int>(type: "int", nullable: false),
                    MaxHealth = table.Column<int>(type: "int", nullable: false),
                    CurrentHealth = table.Column<int>(type: "int", nullable: false),
                    MaxMana = table.Column<int>(type: "int", nullable: false),
                    CurrentMana = table.Column<int>(type: "int", nullable: false),
                    Strength = table.Column<short>(type: "smallint", nullable: false),
                    Agility = table.Column<short>(type: "smallint", nullable: false),
                    Vitality = table.Column<short>(type: "smallint", nullable: false),
                    Spirit = table.Column<short>(type: "smallint", nullable: false),
                    LastLogin = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastLogout = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    LastModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Players", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Players_IsDeleted",
                table: "Players",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_Players_LastLogin",
                table: "Players",
                column: "LastLogin");

            migrationBuilder.CreateIndex(
                name: "IX_Players_Name",
                table: "Players",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Players");
        }
    }
}
