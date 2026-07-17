using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreCode.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddUserEmailAndResetOtp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "Users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ResetOtpAttempts",
                table: "Users",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "ResetOtpExpiresAt",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResetOtpHash",
                table: "Users",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Email",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ResetOtpAttempts",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ResetOtpExpiresAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ResetOtpHash",
                table: "Users");
        }
    }
}
