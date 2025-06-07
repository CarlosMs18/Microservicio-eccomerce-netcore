using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cart.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class addmountaun : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "TotalAmount",
                table: "Carts",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0.00m);

            migrationBuilder.CreateIndex(
                name: "IX_Carts_TotalAmount",
                table: "Carts",
                column: "TotalAmount");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Carts_TotalAmount",
                table: "Carts");

            migrationBuilder.DropColumn(
                name: "TotalAmount",
                table: "Carts");
        }
    }
}
