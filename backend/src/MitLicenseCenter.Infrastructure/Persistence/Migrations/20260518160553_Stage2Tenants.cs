using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MitLicenseCenter.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Stage2Tenants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                schema: "dbo",
                table: "Tenants",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "ActionType",
                schema: "dbo",
                table: "AuditLogs",
                type: "int",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AddColumn<int>(
                name: "Reason",
                schema: "dbo",
                table: "AuditLogs",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_Name",
                schema: "dbo",
                table: "Tenants",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_TenantId",
                schema: "dbo",
                table: "AuditLogs",
                column: "TenantId");

            migrationBuilder.AddForeignKey(
                name: "FK_AuditLogs_Tenants_TenantId",
                schema: "dbo",
                table: "AuditLogs",
                column: "TenantId",
                principalSchema: "dbo",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AuditLogs_Tenants_TenantId",
                schema: "dbo",
                table: "AuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_Tenants_Name",
                schema: "dbo",
                table: "Tenants");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_TenantId",
                schema: "dbo",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                schema: "dbo",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "Reason",
                schema: "dbo",
                table: "AuditLogs");

            migrationBuilder.AlterColumn<string>(
                name: "ActionType",
                schema: "dbo",
                table: "AuditLogs",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");
        }
    }
}
