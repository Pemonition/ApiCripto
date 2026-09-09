using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Carteira.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EstruturaInicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Corretoras",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nome = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Site = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    PaisSede = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    CodigoIntegracao = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    UsuarioId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Corretoras", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Ativos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Simbolo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    MoedaBase = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    MoedaCotacao = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Tipo = table.Column<int>(type: "integer", nullable: false),
                    Monitorado = table.Column<bool>(type: "boolean", nullable: false),
                    UltimoPreco = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: true),
                    Variacao24h = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: true),
                    AtualizadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CorretoraId = table.Column<int>(type: "integer", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uuid", nullable: false)
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
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Preco = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    Variacao24h = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    PrecoMaximo24h = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    PrecoMinimo24h = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    Volume24h = table.Column<decimal>(type: "numeric(24,8)", precision: 24, scale: 8, nullable: false),
                    CapturadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Fonte = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    AtivoId = table.Column<int>(type: "integer", nullable: false)
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
                name: "IX_Ativos_UsuarioId",
                table: "Ativos",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_Corretoras_UsuarioId",
                table: "Corretoras",
                column: "UsuarioId");

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
