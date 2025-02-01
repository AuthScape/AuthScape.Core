using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Services.Database.Migrations
{
    /// <inheritdoc />
    public partial class ProductCardsAdded : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProductCategoryFields");

            migrationBuilder.DropTable(
                name: "ProductFields");

            migrationBuilder.DropTable(
                name: "Products");

            migrationBuilder.DropTable(
                name: "ProductCategories");

            migrationBuilder.CreateTable(
                name: "ProductCardCategories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "newsequentialid()"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductCardCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProductCards",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "newsequentialid()"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Photo = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductCards", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProductCardFields",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "newsequentialid()"),
                    ProductCategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductCardFields", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductCardFields_ProductCardCategories_ProductCategoryId",
                        column: x => x.ProductCategoryId,
                        principalTable: "ProductCardCategories",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ProductCardAndCardFieldMapping",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "newsequentialid()"),
                    ProductFieldId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductCardAndCardFieldMapping", x => new { x.Id, x.ProductId, x.ProductFieldId });
                    table.ForeignKey(
                        name: "FK_ProductCardAndCardFieldMapping_ProductCardFields_ProductFieldId",
                        column: x => x.ProductFieldId,
                        principalTable: "ProductCardFields",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProductCardAndCardFieldMapping_ProductCards_ProductId",
                        column: x => x.ProductId,
                        principalTable: "ProductCards",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductCardAndCardFieldMapping_ProductFieldId",
                table: "ProductCardAndCardFieldMapping",
                column: "ProductFieldId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductCardAndCardFieldMapping_ProductId",
                table: "ProductCardAndCardFieldMapping",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductCardFields_ProductCategoryId",
                table: "ProductCardFields",
                column: "ProductCategoryId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProductCardAndCardFieldMapping");

            migrationBuilder.DropTable(
                name: "ProductCardFields");

            migrationBuilder.DropTable(
                name: "ProductCards");

            migrationBuilder.DropTable(
                name: "ProductCardCategories");

            migrationBuilder.CreateTable(
                name: "ProductCategories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "newsequentialid()"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Products",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "newsequentialid()"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Photo = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProductFields",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "newsequentialid()"),
                    ProductCategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductFields", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductFields_ProductCategories_ProductCategoryId",
                        column: x => x.ProductCategoryId,
                        principalTable: "ProductCategories",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ProductCategoryFields",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "newsequentialid()"),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductFieldId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductCategoryFields", x => new { x.Id, x.ProductId, x.ProductFieldId });
                    table.ForeignKey(
                        name: "FK_ProductCategoryFields_ProductFields_ProductFieldId",
                        column: x => x.ProductFieldId,
                        principalTable: "ProductFields",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProductCategoryFields_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductCategoryFields_ProductFieldId",
                table: "ProductCategoryFields",
                column: "ProductFieldId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductCategoryFields_ProductId",
                table: "ProductCategoryFields",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductFields_ProductCategoryId",
                table: "ProductFields",
                column: "ProductCategoryId");
        }
    }
}
