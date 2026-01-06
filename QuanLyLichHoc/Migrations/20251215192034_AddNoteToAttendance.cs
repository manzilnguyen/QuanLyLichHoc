using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuanLyLichHoc.Migrations
{
    /// <inheritdoc />
    public partial class AddNoteToAttendance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Note",
                table: "Attendances",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Note",
                table: "Attendances");
        }
    }
}
