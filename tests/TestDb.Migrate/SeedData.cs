using Microsoft.EntityFrameworkCore.Migrations;


namespace TestDb.Migrate
{
    public sealed class SeedData : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var bD = new DateTimeOffset(1970, 1, 1, 0, 0, 1,TimeSpan.Zero);
            migrationBuilder.InsertData(
                schema: "my",
                table: "MyTable",
                columns: ["Id", "RowID", "LastChangedBy", "Status", "Code", "Balance", "Heading", "Discount", "RowVersion", "LastChanged", "Created"],
                values: new object[,]
                {
                    { 1L, new Guid("5c1e0d2f-6d9b-4e2e-9f1b-0f1d2c1a1111"), "Baldr", 1, "BD", 350m, "Baldo", 5m, 0, bD, bD },
                    { 2L, new Guid("3c9c9a31-1c1a-4d57-9a6b-9b3d1f2b2222"), "Stefan", 2, "Mn", 200m, "Mando", 0m, 0, bD, bD }
                });

            migrationBuilder.InsertData(
                schema: "my",
                table: "MyTableRef",
                columns: ["Id", "ParentId", "MyInfo", "LastChangedBy", "Amount", "RowVersion", "Created", "LastChanged"],
                values: new object[,]
                {
                    { 1L, 1L, "BigData", "Baldr", 300m, 0, bD, bD },
                    { 2L, 1L, "BiggerData", "Baldr", 50m, 0, bD, bD },
                    { 3L, 2L, "OtherData", "Stefan", 200m, 0, bD, bD }
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "my",
                table: "MyTableRef",
                keyColumn: "Id",
                keyValues: [1L, 2L, 3L]);

            migrationBuilder.DeleteData(
                schema: "my",
                table: "MyTable",
                keyColumn: "Id",
                keyValues: [1L, 2L]);
        }
    }
}
