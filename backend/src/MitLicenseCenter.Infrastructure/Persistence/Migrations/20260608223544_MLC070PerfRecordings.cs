using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MitLicenseCenter.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MLC070PerfRecordings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PerfRecordings",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StoppedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    StopReason = table.Column<int>(type: "int", nullable: true),
                    StartedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PerfRecordings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PerfRecordingSamples",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecordingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SampleUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Measuring = table.Column<bool>(type: "bit", nullable: false),
                    CpuPercent = table.Column<double>(type: "float", nullable: false),
                    CpuQueueLength = table.Column<double>(type: "float", nullable: false),
                    MemoryAvailableMBytes = table.Column<double>(type: "float", nullable: false),
                    MemoryTotalMBytes = table.Column<double>(type: "float", nullable: false),
                    MemoryPagesPerSec = table.Column<double>(type: "float", nullable: false),
                    DiskAvgReadSecPerOp = table.Column<double>(type: "float", nullable: false),
                    DiskAvgWriteSecPerOp = table.Column<double>(type: "float", nullable: false),
                    DiskQueueLength = table.Column<double>(type: "float", nullable: false),
                    ProcessesInaccessible = table.Column<int>(type: "int", nullable: false),
                    ProcessGroupsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OneCLoadJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SqlLoadJson = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PerfRecordingSamples", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PerfRecordingSamples_PerfRecordings_RecordingId",
                        column: x => x.RecordingId,
                        principalSchema: "dbo",
                        principalTable: "PerfRecordings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PerfRecordings_StartedAtUtc",
                schema: "dbo",
                table: "PerfRecordings",
                column: "StartedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_PerfRecordingSamples_RecordingId_SampleUtc",
                schema: "dbo",
                table: "PerfRecordingSamples",
                columns: new[] { "RecordingId", "SampleUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PerfRecordingSamples",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "PerfRecordings",
                schema: "dbo");
        }
    }
}
