using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LayeredTemplate.Auth.Web.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUserFirstLastName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "first_name",
                schema: "auth",
                table: "users",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "last_name",
                schema: "auth",
                table: "users",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "first_name",
                schema: "auth",
                table: "users");

            migrationBuilder.DropColumn(
                name: "last_name",
                schema: "auth",
                table: "users");
        }
    }
}
