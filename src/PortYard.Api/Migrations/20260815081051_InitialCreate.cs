using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PortYard.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Vessels",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Imo = table.Column<string>(type: "TEXT", maxLength: 7, nullable: false),
                    Eta = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vessels", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "YardSlots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Block = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    Row = table.Column<int>(type: "INTEGER", nullable: false),
                    Tier = table.Column<int>(type: "INTEGER", nullable: false),
                    MaxTeu = table.Column<int>(type: "INTEGER", nullable: false),
                    IsReeferCapable = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YardSlots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Containers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ContainerNumber = table.Column<string>(type: "TEXT", maxLength: 11, nullable: false),
                    Size = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Type = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    GrossWeightKg = table.Column<int>(type: "INTEGER", nullable: false),
                    ShippingLine = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    CurrentSlotId = table.Column<int>(type: "INTEGER", nullable: true),
                    InboundVesselId = table.Column<int>(type: "INTEGER", nullable: true),
                    ArrivedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    DepartedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Containers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Containers_Vessels_InboundVesselId",
                        column: x => x.InboundVesselId,
                        principalTable: "Vessels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Containers_YardSlots_CurrentSlotId",
                        column: x => x.CurrentSlotId,
                        principalTable: "YardSlots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CustomsHolds",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ContainerId = table.Column<int>(type: "INTEGER", nullable: false),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    PlacedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ReleasedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomsHolds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomsHolds_Containers_ContainerId",
                        column: x => x.ContainerId,
                        principalTable: "Containers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Movements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ContainerId = table.Column<int>(type: "INTEGER", nullable: false),
                    Type = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    FromSlotId = table.Column<int>(type: "INTEGER", nullable: true),
                    ToSlotId = table.Column<int>(type: "INTEGER", nullable: true),
                    OccurredAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    Operator = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Movements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Movements_Containers_ContainerId",
                        column: x => x.ContainerId,
                        principalTable: "Containers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Containers_ContainerNumber",
                table: "Containers",
                column: "ContainerNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Containers_CurrentSlotId",
                table: "Containers",
                column: "CurrentSlotId");

            migrationBuilder.CreateIndex(
                name: "IX_Containers_InboundVesselId",
                table: "Containers",
                column: "InboundVesselId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomsHolds_ContainerId",
                table: "CustomsHolds",
                column: "ContainerId");

            migrationBuilder.CreateIndex(
                name: "IX_Movements_ContainerId",
                table: "Movements",
                column: "ContainerId");

            migrationBuilder.CreateIndex(
                name: "IX_Vessels_Imo",
                table: "Vessels",
                column: "Imo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_YardSlots_Block_Row_Tier",
                table: "YardSlots",
                columns: new[] { "Block", "Row", "Tier" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomsHolds");

            migrationBuilder.DropTable(
                name: "Movements");

            migrationBuilder.DropTable(
                name: "Containers");

            migrationBuilder.DropTable(
                name: "Vessels");

            migrationBuilder.DropTable(
                name: "YardSlots");
        }
    }
}
