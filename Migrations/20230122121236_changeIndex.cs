using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SkySettings.Migrations
{
    public partial class changeIndex : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "ChangeIndex",
                table: "Settings",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ChangeIndex",
                table: "Settings");
        }
    }
}
