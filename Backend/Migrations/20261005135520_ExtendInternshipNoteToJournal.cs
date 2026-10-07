using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Migrations
{
    /// <inheritdoc />
    public partial class ExtendInternshipNoteToJournal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_InternshipNotes_UserId",
                table: "InternshipNotes");

            migrationBuilder.AlterColumn<string>(
                name: "Content",
                table: "InternshipNotes",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<decimal>(
                name: "HoursSpent",
                table: "InternshipNotes",
                type: "decimal(4,2)",
                precision: 4,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Learned",
                table: "InternshipNotes",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "MentorComment",
                table: "InternshipNotes",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "ReviewedAt",
                table: "InternshipNotes",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReviewedByUserId",
                table: "InternshipNotes",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "InternshipNotes",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "SubmittedAt",
                table: "InternshipNotes",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Tags",
                table: "InternshipNotes",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Title",
                table: "InternshipNotes",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_InternshipNotes_ReviewedByUserId",
                table: "InternshipNotes",
                column: "ReviewedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_InternshipNotes_UserId_Status",
                table: "InternshipNotes",
                columns: new[] { "UserId", "Status" });

            migrationBuilder.AddForeignKey(
                name: "FK_InternshipNotes_Users_ReviewedByUserId",
                table: "InternshipNotes",
                column: "ReviewedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InternshipNotes_Users_ReviewedByUserId",
                table: "InternshipNotes");

            migrationBuilder.DropIndex(
                name: "IX_InternshipNotes_ReviewedByUserId",
                table: "InternshipNotes");

            migrationBuilder.DropIndex(
                name: "IX_InternshipNotes_UserId_Status",
                table: "InternshipNotes");

            migrationBuilder.DropColumn(
                name: "HoursSpent",
                table: "InternshipNotes");

            migrationBuilder.DropColumn(
                name: "Learned",
                table: "InternshipNotes");

            migrationBuilder.DropColumn(
                name: "MentorComment",
                table: "InternshipNotes");

            migrationBuilder.DropColumn(
                name: "ReviewedAt",
                table: "InternshipNotes");

            migrationBuilder.DropColumn(
                name: "ReviewedByUserId",
                table: "InternshipNotes");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "InternshipNotes");

            migrationBuilder.DropColumn(
                name: "SubmittedAt",
                table: "InternshipNotes");

            migrationBuilder.DropColumn(
                name: "Tags",
                table: "InternshipNotes");

            migrationBuilder.DropColumn(
                name: "Title",
                table: "InternshipNotes");

            migrationBuilder.AlterColumn<string>(
                name: "Content",
                table: "InternshipNotes",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(4000)",
                oldMaxLength: 4000);

            migrationBuilder.CreateIndex(
                name: "IX_InternshipNotes_UserId",
                table: "InternshipNotes",
                column: "UserId");
        }
    }
}
