using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace f4872.Migrations
{
    /// <inheritdoc />
    public partial class QuitarReferenciaYFechaDeEntrega : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FechaEntrega",
                table: "Pedidos");

            migrationBuilder.DropColumn(
                name: "Referencia",
                table: "Pedidos");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "FechaEntrega",
                table: "Pedidos",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Referencia",
                table: "Pedidos",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);
        }
    }
}
