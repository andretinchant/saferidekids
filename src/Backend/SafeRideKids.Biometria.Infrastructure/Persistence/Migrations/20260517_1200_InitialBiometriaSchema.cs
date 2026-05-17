using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SafeRideKids.Biometria.Infrastructure.Persistence.Migrations;

/// <summary>
/// Migration inicial do schema 'biometria_poc'. Inclui todas as tabelas
/// definidas na Seção 5 do CONTRACTS.md + 'notification_outbox' (Seção 9).
/// Habilita extensão pgcrypto para suportar pgp_sym_encrypt em colunas
/// de PII (display_name_encrypted).
/// </summary>
[Migration("20260517_1200_InitialBiometriaSchema")]
public partial class InitialBiometriaSchema : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(name: "biometria_poc");

        // pgcrypto: requerido para pgp_sym_encrypt usado em display_name_encrypted.
        migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pgcrypto;");

        migrationBuilder.CreateTable(
            name: "family",
            schema: "biometria_poc",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                responsavel_email_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                preferred_provider_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_family", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "child",
            schema: "biometria_poc",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                family_id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                display_name_encrypted = table.Column<byte[]>(type: "bytea", nullable: false),
                birth_year = table.Column<short>(type: "smallint", nullable: false),
                school_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                boarding_mode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, defaultValue: "manual"),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_child", x => x.id);
                table.ForeignKey(
                    name: "FK_child_family",
                    column: x => x.family_id,
                    principalSchema: "biometria_poc",
                    principalTable: "family",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "consent",
            schema: "biometria_poc",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                family_id = table.Column<Guid>(type: "uuid", nullable: false),
                child_id = table.Column<Guid>(type: "uuid", nullable: false),
                granted_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                responsavel_cpf_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                granted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                scope = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false, defaultValue: "biometria_checkin"),
                consent_text_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                ip_address = table.Column<string>(type: "inet", nullable: false),
                user_agent = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                signed_text_storage_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_consent", x => x.id);
                table.ForeignKey("FK_consent_family", x => x.family_id, "family", "id", "biometria_poc", "biometria_poc", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_consent_child", x => x.child_id, "child", "id", "biometria_poc", "biometria_poc", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "enrollment",
            schema: "biometria_poc",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                child_id = table.Column<Guid>(type: "uuid", nullable: false),
                provider_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                provider_reference_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                template_storage_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                consent_id = table.Column<Guid>(type: "uuid", nullable: false),
                status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                enrolled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_enrollment", x => x.id);
                table.ForeignKey("FK_enrollment_child", x => x.child_id, "child", "id", "biometria_poc", "biometria_poc", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_enrollment_consent", x => x.consent_id, "consent", "id", "biometria_poc", "biometria_poc", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "route",
            schema: "biometria_poc",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                motorista_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                vehicle_plate = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                scheduled_date = table.Column<DateOnly>(type: "date", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_route", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "route_stop",
            schema: "biometria_poc",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                route_id = table.Column<Guid>(type: "uuid", nullable: false),
                child_id = table.Column<Guid>(type: "uuid", nullable: false),
                stop_order = table.Column<short>(type: "smallint", nullable: false),
                expected_pickup_time = table.Column<TimeOnly>(type: "timetz", nullable: false),
                address_label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_route_stop", x => x.id);
                table.ForeignKey("FK_route_stop_route", x => x.route_id, "route", "id", "biometria_poc", "biometria_poc", onDelete: ReferentialAction.Cascade);
                table.ForeignKey("FK_route_stop_child", x => x.child_id, "child", "id", "biometria_poc", "biometria_poc", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "checkin",
            schema: "biometria_poc",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                route_id = table.Column<Guid>(type: "uuid", nullable: false),
                route_stop_id = table.Column<Guid>(type: "uuid", nullable: false),
                child_id = table.Column<Guid>(type: "uuid", nullable: false),
                motorista_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                finished_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                result = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                provider_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                provider_session_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                confidence = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: true),
                liveness_passed = table.Column<bool>(type: "boolean", nullable: true),
                used_fallback = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                fallback_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                latency_ms = table.Column<int>(type: "integer", nullable: true),
                provider_cost_microcents = table.Column<long>(type: "bigint", nullable: true),
                geo_lat = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                geo_lng = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_checkin", x => x.id);
                table.ForeignKey("FK_checkin_route", x => x.route_id, "route", "id", "biometria_poc", "biometria_poc", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_checkin_route_stop", x => x.route_stop_id, "route_stop", "id", "biometria_poc", "biometria_poc", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_checkin_child", x => x.child_id, "child", "id", "biometria_poc", "biometria_poc", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "checkin_event",
            schema: "biometria_poc",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                checkin_id = table.Column<Guid>(type: "uuid", nullable: false),
                event_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                payload = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb"),
                occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_checkin_event", x => x.id);
                table.ForeignKey("FK_checkin_event_checkin", x => x.checkin_id, "checkin", "id", "biometria_poc", "biometria_poc", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "fallback_pin",
            schema: "biometria_poc",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                child_id = table.Column<Guid>(type: "uuid", nullable: false),
                pin_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                valid_from = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                valid_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                attempts = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                consumed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_fallback_pin", x => x.id);
                table.ForeignKey("FK_fallback_pin_child", x => x.child_id, "child", "id", "biometria_poc", "biometria_poc", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "audit_log",
            schema: "biometria_poc",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                actor_id_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                actor_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                action = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                target_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                target_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                payload = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb"),
                occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                retention_until = table.Column<DateOnly>(type: "date", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_audit_log", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "notification_outbox",
            schema: "biometria_poc",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                family_id = table.Column<Guid>(type: "uuid", nullable: false),
                child_id = table.Column<Guid>(type: "uuid", nullable: false),
                checkin_id = table.Column<Guid>(type: "uuid", nullable: true),
                template = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                payload = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb"),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                dispatched_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, defaultValue: "pending")
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_notification_outbox", x => x.id);
            });

        // Indexes ------------------------------------------------------------
        migrationBuilder.CreateIndex("family_tenant_ix", "family", "tenant_id", schema: "biometria_poc");
        migrationBuilder.CreateIndex("family_tenant_email_uk", "family", new[] { "tenant_id", "responsavel_email_hash" }, schema: "biometria_poc", unique: true);

        migrationBuilder.CreateIndex("child_family_ix", "child", "family_id", schema: "biometria_poc");

        migrationBuilder.CreateIndex(
            name: "consent_active_uk",
            schema: "biometria_poc",
            table: "consent",
            column: "child_id",
            unique: true,
            filter: "revoked_at IS NULL");

        migrationBuilder.CreateIndex("enrollment_status_expires_ix", "enrollment", new[] { "status", "expires_at" }, schema: "biometria_poc");
        migrationBuilder.CreateIndex(
            name: "enrollment_active_per_provider",
            schema: "biometria_poc",
            table: "enrollment",
            columns: new[] { "child_id", "provider_id" },
            unique: true,
            filter: "status = 'active'");

        migrationBuilder.CreateIndex("route_motorista_date_ix", "route", new[] { "motorista_id", "scheduled_date" }, schema: "biometria_poc");

        migrationBuilder.CreateIndex(
            name: "route_stop_order_uk",
            schema: "biometria_poc",
            table: "route_stop",
            columns: new[] { "route_id", "stop_order" },
            unique: true);

        migrationBuilder.CreateIndex("checkin_route_ix", "checkin", "route_id", schema: "biometria_poc");
        migrationBuilder.CreateIndex("checkin_started_ix", "checkin", "started_at", schema: "biometria_poc");

        migrationBuilder.CreateIndex("checkin_event_checkin_ix", "checkin_event", "checkin_id", schema: "biometria_poc");

        migrationBuilder.CreateIndex("fallback_pin_child_validity_ix", "fallback_pin", new[] { "child_id", "valid_until" }, schema: "biometria_poc");

        migrationBuilder.CreateIndex("audit_log_tenant_action_ix", "audit_log", new[] { "tenant_id", "action" }, schema: "biometria_poc");
        migrationBuilder.CreateIndex("audit_log_retention_ix", "audit_log", "retention_until", schema: "biometria_poc");

        migrationBuilder.CreateIndex("notification_outbox_status_ix", "notification_outbox", new[] { "status", "created_at" }, schema: "biometria_poc");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("notification_outbox", "biometria_poc");
        migrationBuilder.DropTable("audit_log", "biometria_poc");
        migrationBuilder.DropTable("fallback_pin", "biometria_poc");
        migrationBuilder.DropTable("checkin_event", "biometria_poc");
        migrationBuilder.DropTable("checkin", "biometria_poc");
        migrationBuilder.DropTable("route_stop", "biometria_poc");
        migrationBuilder.DropTable("route", "biometria_poc");
        migrationBuilder.DropTable("enrollment", "biometria_poc");
        migrationBuilder.DropTable("consent", "biometria_poc");
        migrationBuilder.DropTable("child", "biometria_poc");
        migrationBuilder.DropTable("family", "biometria_poc");

        migrationBuilder.Sql("DROP SCHEMA IF EXISTS biometria_poc CASCADE;");
    }
}
