using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreCode.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddResultsReleased : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ResultsReleased",
                table: "Exams",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "ResultsReleasedAt",
                table: "Exams",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ResultsReleased",
                table: "Exams");

            migrationBuilder.DropColumn(
                name: "ResultsReleasedAt",
                table: "Exams");
        }
    }
}
