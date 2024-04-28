using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeAssistantStateMachine.Migrations
{
    /// <inheritdoc />
    public partial class addingMqtt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MqttClientId",
                table: "Variables",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MqttClients",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    Host = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Tls = table.Column<bool>(type: "INTEGER", nullable: false),
                    WebSocket = table.Column<bool>(type: "INTEGER", nullable: false),
                    Username = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    Password = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MqttClients", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Variables_MqttClientId",
                table: "Variables",
                column: "MqttClientId");

            migrationBuilder.AddForeignKey(
                name: "FK_Variables_MqttClients_MqttClientId",
                table: "Variables",
                column: "MqttClientId",
                principalTable: "MqttClients",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Variables_MqttClients_MqttClientId",
                table: "Variables");

            migrationBuilder.DropTable(
                name: "MqttClients");

            migrationBuilder.DropIndex(
                name: "IX_Variables_MqttClientId",
                table: "Variables");

            migrationBuilder.DropColumn(
                name: "MqttClientId",
                table: "Variables");
        }
    }
}
