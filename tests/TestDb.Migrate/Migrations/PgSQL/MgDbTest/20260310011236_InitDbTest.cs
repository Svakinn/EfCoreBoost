using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace TestDb.Migrate.Migrations.PgSQL.MgDbTest
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
                    RowVersion = table.Column<long>(type: "bigint", nullable: false),
                    RowID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    Code = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    Heading = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true),
                    Balance = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Discount = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    LastChanged = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Created = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastChangedBy = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false)
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
                    RowVersion = table.Column<long>(type: "bigint", nullable: false),
                    ParentId = table.Column<long>(type: "bigint", nullable: false),
                    MyInfo = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: false),
                    Created = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastChanged = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastChangedBy = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false)
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
                columns: new[] { "Id", "Balance", "Code", "Created", "Discount", "Heading", "LastChanged", "LastChangedBy", "RowID", "RowVersion", "Status" },
                values: new object[,]
                {
                    { -2L, 200m, "Mn", new DateTimeOffset(new DateTime(2026, 3, 10, 1, 12, 36, 589, DateTimeKind.Unspecified).AddTicks(4094), new TimeSpan(0, 0, 0, 0, 0)), 0m, "Mando", new DateTimeOffset(new DateTime(2026, 3, 10, 1, 12, 36, 589, DateTimeKind.Unspecified).AddTicks(4094), new TimeSpan(0, 0, 0, 0, 0)), "Stefan", new Guid("998cbbc6-7c86-4a55-9b27-a8282c13a6e2"), 0L, 2 },
                    { -1L, 350m, "BD", new DateTimeOffset(new DateTime(2026, 3, 10, 1, 12, 36, 589, DateTimeKind.Unspecified).AddTicks(4069), new TimeSpan(0, 0, 0, 0, 0)), 5m, "Baldo", new DateTimeOffset(new DateTime(2026, 3, 10, 1, 12, 36, 589, DateTimeKind.Unspecified).AddTicks(4068), new TimeSpan(0, 0, 0, 0, 0)), "Baldr", new Guid("5b63dc6d-42b5-4d3e-a259-62b826649579"), 0L, 1 }
                });

            migrationBuilder.InsertData(
                schema: "my",
                table: "MyTableRef",
                columns: new[] { "Id", "Amount", "Created", "LastChanged", "LastChangedBy", "MyInfo", "ParentId", "RowVersion" },
                values: new object[,]
                {
                    { -3L, 200m, new DateTimeOffset(new DateTime(2026, 3, 10, 1, 12, 36, 589, DateTimeKind.Unspecified).AddTicks(4216), new TimeSpan(0, 0, 0, 0, 0)), new DateTimeOffset(new DateTime(2026, 3, 10, 1, 12, 36, 589, DateTimeKind.Unspecified).AddTicks(4216), new TimeSpan(0, 0, 0, 0, 0)), "Stefan", "OtherData", -2L, 0L },
                    { -2L, 50m, new DateTimeOffset(new DateTime(2026, 3, 10, 1, 12, 36, 589, DateTimeKind.Unspecified).AddTicks(4214), new TimeSpan(0, 0, 0, 0, 0)), new DateTimeOffset(new DateTime(2026, 3, 10, 1, 12, 36, 589, DateTimeKind.Unspecified).AddTicks(4214), new TimeSpan(0, 0, 0, 0, 0)), "Baldr", "BiggerData", -1L, 0L },
                    { -1L, 300m, new DateTimeOffset(new DateTime(2026, 3, 10, 1, 12, 36, 589, DateTimeKind.Unspecified).AddTicks(4211), new TimeSpan(0, 0, 0, 0, 0)), new DateTimeOffset(new DateTime(2026, 3, 10, 1, 12, 36, 589, DateTimeKind.Unspecified).AddTicks(4211), new TimeSpan(0, 0, 0, 0, 0)), "Baldr", "BigData", -1L, 0L }
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
