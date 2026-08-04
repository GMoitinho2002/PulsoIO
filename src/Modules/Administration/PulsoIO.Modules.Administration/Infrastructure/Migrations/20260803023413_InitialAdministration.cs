using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PulsoIO.Modules.Administration.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialAdministration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "administration");

            migrationBuilder.CreateTable(
                name: "clients",
                schema: "administration",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_clients", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "environments",
                schema: "administration",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_environments", x => x.Id);
                    table.UniqueConstraint("AK_environments_ClientId_Id", x => new { x.ClientId, x.Id });
                    table.ForeignKey(
                        name: "FK_environments_clients_ClientId",
                        column: x => x.ClientId,
                        principalSchema: "administration",
                        principalTable: "clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "integrations",
                schema: "administration",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    EnvironmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Direction = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SourceSystem = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    TargetSystem = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    HttpMethod = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    EndpointPattern = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_integrations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_integrations_environments_ClientId_EnvironmentId",
                        columns: x => new { x.ClientId, x.EnvironmentId },
                        principalSchema: "administration",
                        principalTable: "environments",
                        principalColumns: new[] { "ClientId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_clients_NormalizedName",
                schema: "administration",
                table: "clients",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_environments_ClientId_NormalizedName",
                schema: "administration",
                table: "environments",
                columns: new[] { "ClientId", "NormalizedName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_integrations_ClientId",
                schema: "administration",
                table: "integrations",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_integrations_ClientId_EnvironmentId",
                schema: "administration",
                table: "integrations",
                columns: new[] { "ClientId", "EnvironmentId" });

            migrationBuilder.CreateIndex(
                name: "IX_integrations_EnvironmentId_NormalizedName",
                schema: "administration",
                table: "integrations",
                columns: new[] { "EnvironmentId", "NormalizedName" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "integrations",
                schema: "administration");

            migrationBuilder.DropTable(
                name: "environments",
                schema: "administration");

            migrationBuilder.DropTable(
                name: "clients",
                schema: "administration");
        }
    }
}
