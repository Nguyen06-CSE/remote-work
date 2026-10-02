using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RemoteWork.Desktop.Persistence.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOfflineSyncQueueFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "SentAt",
                table: "SyncQueue",
                newName: "NextAttemptAt");

            migrationBuilder.RenameColumn(
                name: "RetryCount",
                table: "SyncQueue",
                newName: "IsPermanentFailure");

            migrationBuilder.RenameColumn(
                name: "FailureReason",
                table: "SyncQueue",
                newName: "ErrorMessage");

            migrationBuilder.RenameColumn(
                name: "ItemId",
                table: "SyncQueue",
                newName: "QueueItemId");

            migrationBuilder.AddColumn<int>(
                name: "AttemptCount",
                table: "SyncQueue",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "LastAttemptAt",
                table: "SyncQueue",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SyncQueue_EntityType_EntityId",
                table: "SyncQueue",
                columns: new[] { "EntityType", "EntityId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SyncQueue_EntityType_EntityId",
                table: "SyncQueue");

            migrationBuilder.DropColumn(
                name: "AttemptCount",
                table: "SyncQueue");

            migrationBuilder.DropColumn(
                name: "LastAttemptAt",
                table: "SyncQueue");

            migrationBuilder.RenameColumn(
                name: "NextAttemptAt",
                table: "SyncQueue",
                newName: "SentAt");

            migrationBuilder.RenameColumn(
                name: "IsPermanentFailure",
                table: "SyncQueue",
                newName: "RetryCount");

            migrationBuilder.RenameColumn(
                name: "ErrorMessage",
                table: "SyncQueue",
                newName: "FailureReason");

            migrationBuilder.RenameColumn(
                name: "QueueItemId",
                table: "SyncQueue",
                newName: "ItemId");
        }
    }
}
