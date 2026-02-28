using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DigitalTwin.Infrastructure.Migrations.Local
{
    /// <inheritdoc />
    public partial class AddSleepSessionSyncColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsDirty",
                table: "SleepSessions",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "SyncedAt",
                table: "SleepSessions",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsDirty",
                table: "SleepSessions");

            migrationBuilder.DropColumn(
                name: "SyncedAt",
                table: "SleepSessions");
        }
    }
}
