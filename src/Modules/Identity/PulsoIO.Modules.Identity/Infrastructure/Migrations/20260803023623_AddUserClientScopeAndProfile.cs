using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PulsoIO.Modules.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserClientScopeAndProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ClientId",
                schema: "identity",
                table: "users",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "ProfilePhoto",
                schema: "identity",
                table: "users",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProfilePhotoContentType",
                schema: "identity",
                table: "users",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_client_id",
                schema: "identity",
                table: "users",
                column: "ClientId");

            migrationBuilder.AddForeignKey(
                name: "FK_users_clients_ClientId",
                schema: "identity",
                table: "users",
                column: "ClientId",
                principalSchema: "administration",
                principalTable: "clients",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_users_clients_ClientId",
                schema: "identity",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_users_client_id",
                schema: "identity",
                table: "users");

            migrationBuilder.DropColumn(
                name: "ClientId",
                schema: "identity",
                table: "users");

            migrationBuilder.DropColumn(
                name: "ProfilePhoto",
                schema: "identity",
                table: "users");

            migrationBuilder.DropColumn(
                name: "ProfilePhotoContentType",
                schema: "identity",
                table: "users");
        }
    }
}
