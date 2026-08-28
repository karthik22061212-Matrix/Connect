using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Connect.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCallTimeoutFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "TimeoutDeadline",
                table: "Calls",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<byte>(
                name: "TimeoutType",
                table: "Calls",
                type: "tinyint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Calls_TimeoutDeadline_TimeoutType",
                table: "Calls",
                columns: new[] { "TimeoutDeadline", "TimeoutType" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Calls_TimeoutDeadline_TimeoutType",
                table: "Calls");

            migrationBuilder.DropColumn(
                name: "TimeoutDeadline",
                table: "Calls");

            migrationBuilder.DropColumn(
                name: "TimeoutType",
                table: "Calls");
        }
    }
}
