using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApiCripto.Migrations
{
    /// <inheritdoc />
    public partial class CriandoEntidades : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Corretoras",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Nome = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Site = table.Column<string>(type: "TEXT", nullable: true),
                    PaisSede = table.Column<string>(type: "TEXT", nullable: true),
                    CodigoIntegracao = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Corretoras", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Ativos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Simbolo = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    MoedaBase = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    MoedaCotacao = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    Tipo = table.Column<int>(type: "INTEGER", nullable: false),
                    Monitorado = table.Column<bool>(type: "INTEGER", nullable: false),
                    UltimoPreco = table.Column<double>(type: "REAL", nullable: true),
                    Variacao24h = table.Column<double>(type: "REAL", nullable: true),
                    AtualizadoEm = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CorretoraId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ativos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Ativos_Corretoras_CorretoraId",
                        column: x => x.CorretoraId,
                        principalTable: "Corretoras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Cotacoes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Preco = table.Column<double>(type: "REAL", nullable: false),
                    Variacao24h = table.Column<double>(type: "REAL", nullable: false),
                    PrecoMaximo24h = table.Column<double>(type: "REAL", nullable: false),
                    PrecoMinimo24h = table.Column<double>(type: "REAL", nullable: false),
                    Volume24h = table.Column<double>(type: "REAL", nullable: false),
                    CapturadoEm = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Fonte = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    AtivoId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cotacoes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Cotacoes_Ativos_AtivoId",
                        column: x => x.AtivoId,
                        principalTable: "Ativos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Ativos_CorretoraId_Simbolo",
                table: "Ativos",
                columns: new[] { "CorretoraId", "Simbolo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Cotacoes_AtivoId_CapturadoEm",
                table: "Cotacoes",
                columns: new[] { "AtivoId", "CapturadoEm" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Cotacoes");

            migrationBuilder.DropTable(
                name: "Ativos");

            migrationBuilder.DropTable(
                name: "Corretoras");
        }
    }
}
