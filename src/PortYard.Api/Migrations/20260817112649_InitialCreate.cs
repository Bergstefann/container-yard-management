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
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Imo = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: false),
                    Eta = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vessels", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "YardSlots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Block = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Row = table.Column<int>(type: "int", nullable: false),
                    Tier = table.Column<int>(type: "int", nullable: false),
                    MaxTeu = table.Column<int>(type: "int", nullable: false),
                    IsReeferCapable = table.Column<bool>(type: "bit", nullable: false),
                    LastModifiedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YardSlots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Containers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ContainerNumber = table.Column<string>(type: "nvarchar(11)", maxLength: 11, nullable: false),
                    Size = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    GrossWeightKg = table.Column<int>(type: "int", nullable: false),
                    ShippingLine = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CurrentSlotId = table.Column<int>(type: "int", nullable: true),
                    InboundVesselId = table.Column<int>(type: "int", nullable: true),
                    ArrivedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DepartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
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
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ContainerId = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    PlacedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ReleasedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
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
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ContainerId = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FromSlotRefId = table.Column<int>(type: "int", nullable: true),
                    ToSlotRefId = table.Column<int>(type: "int", nullable: true),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Operator = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
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
                    table.ForeignKey(
                        name: "FK_Movements_YardSlots_FromSlotRefId",
                        column: x => x.FromSlotRefId,
                        principalTable: "YardSlots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Movements_YardSlots_ToSlotRefId",
                        column: x => x.ToSlotRefId,
                        principalTable: "YardSlots",
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
                name: "IX_Movements_FromSlotRefId",
                table: "Movements",
                column: "FromSlotRefId");

            migrationBuilder.CreateIndex(
                name: "IX_Movements_ToSlotRefId",
                table: "Movements",
                column: "ToSlotRefId");

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
