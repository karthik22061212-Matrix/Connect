using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Connect.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCanonicalConnectRequestUniqueness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ConnectRequests_FromUserId_ToUserId",
                table: "ConnectRequests");

            migrationBuilder.AddColumn<Guid>(
                name: "CanonicalUserAId",
                table: "ConnectRequests",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CanonicalUserBId",
                table: "ConnectRequests",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.Sql(@"
                UPDATE ConnectRequests
                SET CanonicalUserAId = CASE WHEN FromUserId < ToUserId THEN FromUserId ELSE ToUserId END,
                    CanonicalUserBId = CASE WHEN FromUserId < ToUserId THEN ToUserId ELSE FromUserId END
            ");

            migrationBuilder.CreateIndex(
                name: "IX_ConnectRequests_CanonicalUserAId_CanonicalUserBId",
                table: "ConnectRequests",
                columns: new[] { "CanonicalUserAId", "CanonicalUserBId" },
                unique: true,
                filter: "[Status] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ConnectRequests_FromUserId",
                table: "ConnectRequests",
                column: "FromUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ConnectRequests_CanonicalUserAId_CanonicalUserBId",
                table: "ConnectRequests");

            migrationBuilder.DropIndex(
                name: "IX_ConnectRequests_FromUserId",
                table: "ConnectRequests");

            migrationBuilder.DropColumn(
                name: "CanonicalUserAId",
                table: "ConnectRequests");

            migrationBuilder.DropColumn(
                name: "CanonicalUserBId",
                table: "ConnectRequests");

            migrationBuilder.CreateIndex(
                name: "IX_ConnectRequests_FromUserId_ToUserId",
                table: "ConnectRequests",
                columns: new[] { "FromUserId", "ToUserId" },
                unique: true,
                filter: "[Status] = 0");
        }
    }
}
