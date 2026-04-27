using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CC.Infraestructure.AdminDbMigrations
{
    /// <inheritdoc />
    public partial class AddNotificationModulePhase1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NotificationDeliveryLogs",
                schema: "admin",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    EventCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TemplateCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Channel = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Recipient = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    FromEmail = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    FromName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ReplyTo = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    Subject = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Provider = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ProviderMessageId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ErrorCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ConsumedCredits = table.Column<int>(type: "integer", nullable: false),
                    ReferenceType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ReferenceId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationDeliveryLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NotificationDeliveryLogs_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "admin",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "NotificationEventDefinitions",
                schema: "admin",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Channel = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IsTenantConfigurable = table.Column<bool>(type: "boolean", nullable: false),
                    IsSystemRequired = table.Column<bool>(type: "boolean", nullable: false),
                    ConsumesQuota = table.Column<bool>(type: "boolean", nullable: false),
                    DefaultEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    TemplateCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationEventDefinitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NotificationTemplates",
                schema: "admin",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Channel = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SourceType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SubjectTemplate = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    HtmlTemplate = table.Column<string>(type: "text", nullable: true),
                    TextTemplate = table.Column<string>(type: "text", nullable: true),
                    AvailableVariablesJson = table.Column<string>(type: "jsonb", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TenantNotificationCreditLedgers",
                schema: "admin",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Channel = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    MovementType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ReferenceType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ReferenceId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PeriodYear = table.Column<int>(type: "integer", nullable: false),
                    PeriodMonth = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantNotificationCreditLedgers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TenantNotificationCreditLedgers_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "admin",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TenantNotificationPreferences",
                schema: "admin",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Channel = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantNotificationPreferences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TenantNotificationPreferences_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "admin",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TenantNotificationQuotas",
                schema: "admin",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    PeriodYear = table.Column<int>(type: "integer", nullable: false),
                    PeriodMonth = table.Column<int>(type: "integer", nullable: false),
                    IncludedEmailCredits = table.Column<int>(type: "integer", nullable: false),
                    PurchasedEmailCredits = table.Column<int>(type: "integer", nullable: false),
                    UsedEmailCredits = table.Column<int>(type: "integer", nullable: false),
                    ReservedEmailCredits = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantNotificationQuotas", x => x.Id);
                    table.CheckConstraint("CK_TenantNotificationQuota_ReservedEmailCredits_NonNegative", "\"ReservedEmailCredits\" >= 0");
                    table.CheckConstraint("CK_TenantNotificationQuota_UsedEmailCredits_NonNegative", "\"UsedEmailCredits\" >= 0");
                    table.ForeignKey(
                        name: "FK_TenantNotificationQuotas_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "admin",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationDeliveryLogs_EventCode_Status",
                schema: "admin",
                table: "NotificationDeliveryLogs",
                columns: new[] { "EventCode", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationDeliveryLogs_TenantId_CreatedAt",
                schema: "admin",
                table: "NotificationDeliveryLogs",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationEventDefinitions_Code_Channel",
                schema: "admin",
                table: "NotificationEventDefinitions",
                columns: new[] { "Code", "Channel" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NotificationTemplates_Code_Channel_Version",
                schema: "admin",
                table: "NotificationTemplates",
                columns: new[] { "Code", "Channel", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TenantNotificationCreditLedgers_TenantId_PeriodYear_PeriodM~",
                schema: "admin",
                table: "TenantNotificationCreditLedgers",
                columns: new[] { "TenantId", "PeriodYear", "PeriodMonth" });

            migrationBuilder.CreateIndex(
                name: "IX_TenantNotificationPreferences_TenantId_EventCode_Channel",
                schema: "admin",
                table: "TenantNotificationPreferences",
                columns: new[] { "TenantId", "EventCode", "Channel" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TenantNotificationQuotas_TenantId_PeriodYear_PeriodMonth",
                schema: "admin",
                table: "TenantNotificationQuotas",
                columns: new[] { "TenantId", "PeriodYear", "PeriodMonth" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NotificationDeliveryLogs",
                schema: "admin");

            migrationBuilder.DropTable(
                name: "NotificationEventDefinitions",
                schema: "admin");

            migrationBuilder.DropTable(
                name: "NotificationTemplates",
                schema: "admin");

            migrationBuilder.DropTable(
                name: "TenantNotificationCreditLedgers",
                schema: "admin");

            migrationBuilder.DropTable(
                name: "TenantNotificationPreferences",
                schema: "admin");

            migrationBuilder.DropTable(
                name: "TenantNotificationQuotas",
                schema: "admin");
        }
    }
}
