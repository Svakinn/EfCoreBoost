using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace BoostTest.TestDb.Migrations.PgSQL.MgDbTest
{
    /// <inheritdoc />
    public partial class InitDbTest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "my");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:citext", ",,")
                .Annotation("Npgsql:PostgresExtension:pgcrypto", ",,");

            migrationBuilder.CreateTable(
                name: "MyTable",
                schema: "my",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RowID = table.Column<Guid>(type: "uuid", nullable: false),
                    LastChanged = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastChangedBy = table.Column<string>(type: "citext", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MyTable", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MyTableRef",
                schema: "my",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ParentId = table.Column<long>(type: "bigint", nullable: false),
                    MyInfo = table.Column<string>(type: "citext", nullable: false),
                    LastChanged = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastChangedBy = table.Column<string>(type: "citext", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MyTableRef", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MyTableRef_MyTable_ParentId",
                        column: x => x.ParentId,
                        principalSchema: "my",
                        principalTable: "MyTable",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                schema: "my",
                table: "MyTable",
                columns: new[] { "Id", "LastChanged", "LastChangedBy", "RowID" },
                values: new object[,]
                {
                    { -2L, new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Stefan", new Guid("caa5a062-c9bb-494f-bf9d-538e64f20b6a") },
                    { -1L, new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Baldr", new Guid("ac4b0a74-e0b6-42eb-b11c-71ac0db378be") }
                });

            migrationBuilder.InsertData(
                schema: "my",
                table: "MyTableRef",
                columns: new[] { "Id", "LastChanged", "LastChangedBy", "MyInfo", "ParentId" },
                values: new object[,]
                {
                    { -3L, new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Stefan", "OtherData", -2L },
                    { -2L, new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Baldr", "BiggerData", -1L },
                    { -1L, new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Baldr", "BigData", -1L }
                });

            migrationBuilder.CreateIndex(
                name: "IX_MyTable_LastChanged",
                schema: "my",
                table: "MyTable",
                column: "LastChanged");

            migrationBuilder.CreateIndex(
                name: "IX_MyTable_RowID",
                schema: "my",
                table: "MyTable",
                column: "RowID",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MyTableRef_MyInfo",
                schema: "my",
                table: "MyTableRef",
                column: "MyInfo");

            migrationBuilder.CreateIndex(
                name: "IX_MyTableRef_ParentId",
                schema: "my",
                table: "MyTableRef",
                column: "ParentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MyTableRef",
                schema: "my");

            migrationBuilder.DropTable(
                name: "MyTable",
                schema: "my");
        }
    }
}
