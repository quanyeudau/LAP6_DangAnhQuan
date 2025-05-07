using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ASC.Web.Migrations
{
    public partial class MyDatabase : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Tạo bảng MasterDataKeys
            migrationBuilder.CreateTable(
                name: "MasterDataKeys",
                columns: table => new
                {
                    PartitionKey = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    RowKey = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MasterDataKeys", x => new { x.PartitionKey, x.RowKey });
                });

            // Tạo bảng MasterDataValues
            migrationBuilder.CreateTable(
                name: "MasterDataValues",
                columns: table => new
                {
                    PartitionKey = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    RowKey = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MasterDataValues", x => new { x.PartitionKey, x.RowKey });
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Xóa bảng MasterDataValues trước để không vi phạm khóa ngoại nếu có
            migrationBuilder.DropTable(
                name: "MasterDataValues");

            // Xóa bảng MasterDataKeys
            migrationBuilder.DropTable(
                name: "MasterDataKeys");
        }
    }
}
