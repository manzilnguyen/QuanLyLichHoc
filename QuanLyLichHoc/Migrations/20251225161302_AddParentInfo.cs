using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuanLyLichHoc.Migrations
{
    /// <inheritdoc />
    public partial class AddParentInfo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Lecturers_AppUsers_AppUserId",
                table: "Lecturers");

            migrationBuilder.DropIndex(
                name: "IX_Lecturers_AppUserId",
                table: "Lecturers");

            migrationBuilder.DropIndex(
                name: "IX_AppUsers_LecturerId",
                table: "AppUsers");

            migrationBuilder.DropIndex(
                name: "IX_AppUsers_StudentId",
                table: "AppUsers");

            migrationBuilder.DropColumn(
                name: "AppUserId",
                table: "Lecturers");

            migrationBuilder.AddColumn<string>(
                name: "ParentName",
                table: "Students",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ParentPhone",
                table: "Students",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppUsers_LecturerId",
                table: "AppUsers",
                column: "LecturerId",
                unique: true,
                filter: "[LecturerId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AppUsers_StudentId",
                table: "AppUsers",
                column: "StudentId",
                unique: true,
                filter: "[StudentId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppUsers_LecturerId",
                table: "AppUsers");

            migrationBuilder.DropIndex(
                name: "IX_AppUsers_StudentId",
                table: "AppUsers");

            migrationBuilder.DropColumn(
                name: "ParentName",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "ParentPhone",
                table: "Students");

            migrationBuilder.AddColumn<int>(
                name: "AppUserId",
                table: "Lecturers",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Lecturers_AppUserId",
                table: "Lecturers",
                column: "AppUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AppUsers_LecturerId",
                table: "AppUsers",
                column: "LecturerId");

            migrationBuilder.CreateIndex(
                name: "IX_AppUsers_StudentId",
                table: "AppUsers",
                column: "StudentId");

            migrationBuilder.AddForeignKey(
                name: "FK_Lecturers_AppUsers_AppUserId",
                table: "Lecturers",
                column: "AppUserId",
                principalTable: "AppUsers",
                principalColumn: "Id");
        }
    }
}
