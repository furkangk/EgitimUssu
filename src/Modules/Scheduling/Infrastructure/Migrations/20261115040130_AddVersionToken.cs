using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgitimUssu.Modules.Scheduling.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVersionToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // xmin PostgreSQL sistem kolonudur; Npgsql bu işlemler için DDL üretmez.
            // Sürüm eşlemesi yalnız Designer/Snapshot metadatasında saklanır.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // xmin PostgreSQL sistem kolonudur; Npgsql bu işlemler için DDL üretmez.
            // Sürüm eşlemesi yalnız Designer/Snapshot metadatasında saklanır.
        }
    }
}
