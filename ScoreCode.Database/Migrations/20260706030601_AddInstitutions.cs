using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreCode.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddInstitutions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "InstitutionId",
                table: "Users",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "InstitutionId",
                table: "Submissions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "InstitutionId",
                table: "Sessions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "InstitutionId",
                table: "Exams",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Institutions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Prefix = table.Column<string>(type: "text", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    ContactEmail = table.Column<string>(type: "text", nullable: false),
                    ContactPerson = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ApprovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RejectionReason = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Institutions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_InstitutionId",
                table: "Users",
                column: "InstitutionId");

            migrationBuilder.CreateIndex(
                name: "IX_Submissions_InstitutionId",
                table: "Submissions",
                column: "InstitutionId");

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_InstitutionId",
                table: "Sessions",
                column: "InstitutionId");

            migrationBuilder.CreateIndex(
                name: "IX_Exams_InstitutionId",
                table: "Exams",
                column: "InstitutionId");

            migrationBuilder.AddForeignKey(
                name: "FK_Exams_Institutions_InstitutionId",
                table: "Exams",
                column: "InstitutionId",
                principalTable: "Institutions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Sessions_Institutions_InstitutionId",
                table: "Sessions",
                column: "InstitutionId",
                principalTable: "Institutions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Submissions_Institutions_InstitutionId",
                table: "Submissions",
                column: "InstitutionId",
                principalTable: "Institutions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Institutions_InstitutionId",
                table: "Users",
                column: "InstitutionId",
                principalTable: "Institutions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Exams_Institutions_InstitutionId",
                table: "Exams");

            migrationBuilder.DropForeignKey(
                name: "FK_Sessions_Institutions_InstitutionId",
                table: "Sessions");

            migrationBuilder.DropForeignKey(
                name: "FK_Submissions_Institutions_InstitutionId",
                table: "Submissions");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_Institutions_InstitutionId",
                table: "Users");

            migrationBuilder.DropTable(
                name: "Institutions");

            migrationBuilder.DropIndex(
                name: "IX_Users_InstitutionId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Submissions_InstitutionId",
                table: "Submissions");

            migrationBuilder.DropIndex(
                name: "IX_Sessions_InstitutionId",
                table: "Sessions");

            migrationBuilder.DropIndex(
                name: "IX_Exams_InstitutionId",
                table: "Exams");

            migrationBuilder.DropColumn(
                name: "InstitutionId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "InstitutionId",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "InstitutionId",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "InstitutionId",
                table: "Exams");
        }
    }
}
