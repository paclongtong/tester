using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace friction_tester.Migrations
{
    /// <inheritdoc />
    public partial class RemoveAdditionalInfoColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "test_results",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    test_name = table.Column<string>(type: "text", nullable: false),
                    workpiece_name = table.Column<string>(type: "text", nullable: false),
                    operator_name = table.Column<string>(type: "text", nullable: false),
                    timestamp = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    velocity = table.Column<float>(type: "real", nullable: false),
                    acceleration = table.Column<float>(type: "real", nullable: false),
                    start_position = table.Column<float>(type: "real", nullable: false),
                    end_position = table.Column<float>(type: "real", nullable: false),
                    total_duration = table.Column<double>(type: "double precision", nullable: false),
                    distance_covered = table.Column<float>(type: "real", nullable: false),
                    anomalies_detected = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_test_results", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sensor_data",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    test_id = table.Column<int>(type: "integer", nullable: false),
                    timestamp = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    position = table.Column<double>(type: "double precision", nullable: false),
                    sensor_value = table.Column<double>(type: "double precision", nullable: false),
                    AdditionalInfo = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sensor_data", x => x.id);
                    table.ForeignKey(
                        name: "FK_sensor_data_test_results_test_id",
                        column: x => x.test_id,
                        principalTable: "test_results",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_sensor_data_test_id",
                table: "sensor_data",
                column: "test_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sensor_data");

            migrationBuilder.DropTable(
                name: "test_results");
        }
    }
}
