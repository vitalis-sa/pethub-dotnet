using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vitalis.Migrations
{
    /// <inheritdoc />
    public partial class InicialVitalis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TB_RESPONSAVEL",
                columns: table => new
                {
                    ID = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    NOME = table.Column<string>(type: "NVARCHAR2(150)", maxLength: 150, nullable: false),
                    CPF = table.Column<string>(type: "NVARCHAR2(11)", maxLength: 11, nullable: false),
                    EMAIL = table.Column<string>(type: "NVARCHAR2(2000)", nullable: false),
                    SENHA = table.Column<string>(type: "NVARCHAR2(2000)", nullable: false),
                    ATIVO = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    CREATED_AT = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false, defaultValueSql: "SYSDATE")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TB_RESPONSAVEL", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "TB_LEMBRETE",
                columns: table => new
                {
                    ID = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    RESPONSAVEL_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    PET_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    TIPO = table.Column<string>(type: "NVARCHAR2(2000)", nullable: false),
                    DATA_AGENDADA = table.Column<string>(type: "NVARCHAR2(10)", nullable: false),
                    DATA_HORA = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    MENSAGEM = table.Column<string>(type: "NVARCHAR2(2000)", nullable: false),
                    STATUS = table.Column<string>(type: "NVARCHAR2(2000)", nullable: false),
                    REFERENCIA_ID = table.Column<long>(type: "NUMBER(19)", nullable: true),
                    REFERENCIA_TIPO = table.Column<string>(type: "NVARCHAR2(2000)", nullable: true),
                    CREATED_AT = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false, defaultValueSql: "SYSDATE")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TB_LEMBRETE", x => x.ID);
                    table.ForeignKey(
                        name: "FK_TB_LEMBRETE_TB_RESPONSAVEL_RESPONSAVEL_ID",
                        column: x => x.RESPONSAVEL_ID,
                        principalTable: "TB_RESPONSAVEL",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TB_RESPONSAVEL_CONTATO",
                columns: table => new
                {
                    ID = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    RESPONSAVEL_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    TIPO = table.Column<string>(type: "NVARCHAR2(2000)", nullable: false),
                    TELEFONE = table.Column<string>(type: "NVARCHAR2(2000)", nullable: false),
                    PRINCIPAL = table.Column<bool>(type: "NUMBER(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TB_RESPONSAVEL_CONTATO", x => x.ID);
                    table.ForeignKey(
                        name: "FK_TB_RESPONSAVEL_CONTATO_TB_RESPONSAVEL_RESPONSAVEL_ID",
                        column: x => x.RESPONSAVEL_ID,
                        principalTable: "TB_RESPONSAVEL",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TB_RESPONSAVEL_ENDERECO",
                columns: table => new
                {
                    ID = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    RESPONSAVEL_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    LOGRADOURO = table.Column<string>(type: "NVARCHAR2(2000)", nullable: false),
                    NUMERO = table.Column<string>(type: "NVARCHAR2(2000)", nullable: false),
                    COMPLEMENTO = table.Column<string>(type: "NVARCHAR2(2000)", nullable: true),
                    BAIRRO = table.Column<string>(type: "NVARCHAR2(2000)", nullable: false),
                    CIDADE = table.Column<string>(type: "NVARCHAR2(2000)", nullable: false),
                    ESTADO = table.Column<string>(type: "NVARCHAR2(2)", maxLength: 2, nullable: false),
                    CEP = table.Column<string>(type: "NVARCHAR2(8)", maxLength: 8, nullable: false),
                    PRINCIPAL = table.Column<bool>(type: "NUMBER(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TB_RESPONSAVEL_ENDERECO", x => x.ID);
                    table.ForeignKey(
                        name: "FK_TB_RESPONSAVEL_ENDERECO_TB_RESPONSAVEL_RESPONSAVEL_ID",
                        column: x => x.RESPONSAVEL_ID,
                        principalTable: "TB_RESPONSAVEL",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TB_LEMBRETE_RESPONSAVEL_ID",
                table: "TB_LEMBRETE",
                column: "RESPONSAVEL_ID");

            migrationBuilder.CreateIndex(
                name: "IX_TB_RESPONSAVEL_CONTATO_RESPONSAVEL_ID",
                table: "TB_RESPONSAVEL_CONTATO",
                column: "RESPONSAVEL_ID");

            migrationBuilder.CreateIndex(
                name: "IX_TB_RESPONSAVEL_ENDERECO_RESPONSAVEL_ID",
                table: "TB_RESPONSAVEL_ENDERECO",
                column: "RESPONSAVEL_ID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TB_LEMBRETE");

            migrationBuilder.DropTable(
                name: "TB_RESPONSAVEL_CONTATO");

            migrationBuilder.DropTable(
                name: "TB_RESPONSAVEL_ENDERECO");

            migrationBuilder.DropTable(
                name: "TB_RESPONSAVEL");
        }
    }
}
