using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MitLicenseCenter.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MLC237InvestigationModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // MLC-237: Investigation ЗАМЕНЯЕТ TechLogCollection (MLC-230). Порядок: создать новые таблицы
            // → ПЕРЕНЕСТИ данные из TechLogCollections → удалить старую таблицу. CreateTable идёт ПЕРЕД
            // DropTable (EF по умолчанию scaffold'ит обратный порядок — переставлено вручную ради
            // INSERT…SELECT между ними).
            migrationBuilder.CreateTable(
                name: "Investigations",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Scenario = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StoppedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    StopReason = table.Column<int>(type: "int", nullable: true),
                    StartedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    InfobaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    InfobaseProcessName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CollectionDirectory = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    ConfigMarker = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    Config_LogcfgLocation = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    Config_Events = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    Config_DurationThresholdMicros = table.Column<long>(type: "bigint", nullable: true),
                    Config_ProcessNameFilter = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Config_Format = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    Config_HistoryHours = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Investigations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Findings",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InvestigationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    SchemaVersion = table.Column<int>(type: "int", nullable: false),
                    ResultJson = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Findings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Findings_Investigations_InvestigationId",
                        column: x => x.InvestigationId,
                        principalSchema: "dbo",
                        principalTable: "Investigations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Findings_InvestigationId",
                schema: "dbo",
                table: "Findings",
                column: "InvestigationId");

            migrationBuilder.CreateIndex(
                name: "IX_Investigations_StartedAtUtc",
                schema: "dbo",
                table: "Investigations",
                column: "StartedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Investigations_Status",
                schema: "dbo",
                table: "Investigations",
                column: "Status");

            // Перенос данных TechLogCollections → Investigations (раздельные контракты, маппинг полей):
            //   • Scenario: строка → int по имени (TechLogScenario.ToString() ↔ InvestigationScenario):
            //     Locks→0, SlowQueries→1, Exceptions→2, GeneralSlow→3, DbmsLocks→4 (неизвестное → 0).
            //   • Status: Active(0)→Collecting(0), Stopped(1)→Completed(2), Interrupted(2)→Interrupted(3).
            //   • StopReason: Manual(0)/TimeLimit(1)/DiskLimit(2) — без изменений (0/1/2 совместимы).
            //   • StartedBy: у старой сущности поля не было → 'system' (NOT NULL).
            //   • TenantId/InfobaseId/Config_*/RowVersion: NULL (RowVersion проставит сервер при первом UPDATE).
            migrationBuilder.Sql(@"
INSERT INTO [dbo].[Investigations]
    ([Id], [Scenario], [Status], [StartedAtUtc], [StoppedAtUtc], [StopReason],
     [StartedBy], [InfobaseProcessName], [CollectionDirectory], [ConfigMarker])
SELECT
    [Id],
    CASE [Scenario]
        WHEN 'Locks' THEN 0
        WHEN 'SlowQueries' THEN 1
        WHEN 'Exceptions' THEN 2
        WHEN 'GeneralSlow' THEN 3
        WHEN 'DbmsLocks' THEN 4
        ELSE 0
    END,
    CASE [Status]
        WHEN 0 THEN 0   -- Active      → Collecting
        WHEN 1 THEN 2   -- Stopped     → Completed
        WHEN 2 THEN 3   -- Interrupted → Interrupted
        ELSE 3
    END,
    [StartedAtUtc],
    [StoppedAtUtc],
    [StopReason],
    N'system',
    [InfobaseProcessName],
    [CollectionDirectory],
    [ConfigMarker]
FROM [dbo].[TechLogCollections];
");

            migrationBuilder.DropTable(
                name: "TechLogCollections",
                schema: "dbo");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Откат: пересоздать TechLogCollections и вернуть данные (обратный маппинг). Findings/owned
            // CollectionConfig/привязки арендатора у старой сущности отсутствуют → теряются (это откат
            // именно структуры этапа C). Порядок: создать старую таблицу → INSERT…SELECT → удалить новые.
            migrationBuilder.CreateTable(
                name: "TechLogCollections",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CollectionDirectory = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    ConfigMarker = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    InfobaseProcessName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Scenario = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    StopReason = table.Column<int>(type: "int", nullable: true),
                    StoppedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TechLogCollections", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TechLogCollections_StartedAtUtc",
                schema: "dbo",
                table: "TechLogCollections",
                column: "StartedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_TechLogCollections_Status",
                schema: "dbo",
                table: "TechLogCollections",
                column: "Status");

            // Обратный перенос данных (Investigations → TechLogCollections). Scenario: int → строка;
            // Status: Collecting(0)→Active(0), Completed(2)→Stopped(1), Interrupted(3)→Interrupted(2),
            // Analyzing(1)/Failed(4) (новые состояния этапа C) → Active(0)/Stopped(1) как ближайший аналог.
            migrationBuilder.Sql(@"
INSERT INTO [dbo].[TechLogCollections]
    ([Id], [Scenario], [Status], [StartedAtUtc], [StoppedAtUtc], [StopReason],
     [InfobaseProcessName], [CollectionDirectory], [ConfigMarker])
SELECT
    [Id],
    CASE [Scenario]
        WHEN 0 THEN N'Locks'
        WHEN 1 THEN N'SlowQueries'
        WHEN 2 THEN N'Exceptions'
        WHEN 3 THEN N'GeneralSlow'
        WHEN 4 THEN N'DbmsLocks'
        ELSE N'Locks'
    END,
    CASE [Status]
        WHEN 0 THEN 0   -- Collecting  → Active
        WHEN 1 THEN 0   -- Analyzing   → Active (ближайший аналог)
        WHEN 2 THEN 1   -- Completed   → Stopped
        WHEN 3 THEN 2   -- Interrupted → Interrupted
        WHEN 4 THEN 1   -- Failed      → Stopped (ближайший аналог)
        ELSE 0
    END,
    [StartedAtUtc],
    [StoppedAtUtc],
    CASE WHEN [StopReason] = 3 THEN 0 ELSE [StopReason] END,  -- Error(3) → Manual(0); старый enum его не знал
    [InfobaseProcessName],
    [CollectionDirectory],
    [ConfigMarker]
FROM [dbo].[Investigations];
");

            migrationBuilder.DropTable(
                name: "Findings",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Investigations",
                schema: "dbo");
        }
    }
}
