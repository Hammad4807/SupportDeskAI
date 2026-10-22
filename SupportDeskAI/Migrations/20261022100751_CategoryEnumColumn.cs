using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SupportDeskAI.Migrations
{
    /// <inheritdoc />
    public partial class CategoryEnumColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "Category",
                table: "Tickets",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_Category",
                table: "Tickets",
                column: "Category");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tickets_Category",
                table: "Tickets");

            migrationBuilder.AlterColumn<string>(
                name: "Category",
                table: "Tickets",
                type: "TEXT",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);
        }
    }
}
