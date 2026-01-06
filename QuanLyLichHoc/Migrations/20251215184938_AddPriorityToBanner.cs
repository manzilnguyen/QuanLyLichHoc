using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuanLyLichHoc.Migrations
{
    /// <inheritdoc />
    public partial class AddPriorityToBanner : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Priority",
                table: "Banners",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Priority",
                table: "Banners");
        }
    }
}
