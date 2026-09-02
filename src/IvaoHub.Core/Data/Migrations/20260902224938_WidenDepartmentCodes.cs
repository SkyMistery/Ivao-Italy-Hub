using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IvaoHub.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class WidenDepartmentCodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "department",
                table: "hub_user_staff_positions",
                type: "varchar(4)",
                maxLength: 4,
                nullable: true,
                collation: "utf8mb4_unicode_ci",
                oldClrType: typeof(string),
                oldType: "varchar(2)",
                oldMaxLength: 2,
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("Relational:Collation", "utf8mb4_unicode_ci");

            migrationBuilder.AlterColumn<string>(
                name: "department",
                table: "hub_user_grants",
                type: "varchar(4)",
                maxLength: 4,
                nullable: true,
                collation: "utf8mb4_unicode_ci",
                oldClrType: typeof(string),
                oldType: "varchar(2)",
                oldMaxLength: 2,
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("Relational:Collation", "utf8mb4_unicode_ci");

            migrationBuilder.AlterColumn<string>(
                name: "owner_department",
                table: "cms_search_index",
                type: "varchar(4)",
                maxLength: 4,
                nullable: false,
                collation: "utf8mb4_unicode_ci",
                oldClrType: typeof(string),
                oldType: "varchar(2)",
                oldMaxLength: 2)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("Relational:Collation", "utf8mb4_unicode_ci");

            migrationBuilder.AlterColumn<string>(
                name: "owner_department",
                table: "cms_links",
                type: "varchar(4)",
                maxLength: 4,
                nullable: false,
                collation: "utf8mb4_unicode_ci",
                oldClrType: typeof(string),
                oldType: "varchar(2)",
                oldMaxLength: 2)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("Relational:Collation", "utf8mb4_unicode_ci");

            migrationBuilder.AlterColumn<string>(
                name: "owner_department",
                table: "cms_contents",
                type: "varchar(4)",
                maxLength: 4,
                nullable: false,
                collation: "utf8mb4_unicode_ci",
                oldClrType: typeof(string),
                oldType: "varchar(2)",
                oldMaxLength: 2)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("Relational:Collation", "utf8mb4_unicode_ci");

            migrationBuilder.AlterColumn<string>(
                name: "owner_department",
                table: "cms_calendar_entries",
                type: "varchar(4)",
                maxLength: 4,
                nullable: false,
                collation: "utf8mb4_unicode_ci",
                oldClrType: typeof(string),
                oldType: "varchar(2)",
                oldMaxLength: 2)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("Relational:Collation", "utf8mb4_unicode_ci");

            // The codes themselves change, not just the width: these are the department
            // abbreviations IVAO uses, and they are not a mechanical suffix (ATC operations is
            // AOD, training is TD). Rows written before this migration are rewritten in place.
            migrationBuilder.Sql(
                "UPDATE `hub_user_staff_positions` SET `department` = CASE `department` WHEN 'SO' THEN 'SOD' WHEN 'FO' THEN 'FOD' WHEN 'AO' THEN 'AOD' WHEN 'TR' THEN 'TD' WHEN 'MB' THEN 'MD' WHEN 'EV' THEN 'ED' WHEN 'PR' THEN 'PRD' WHEN 'WM' THEN 'WD' ELSE `department` END WHERE `department` IN ('SO', 'FO', 'AO', 'TR', 'MB', 'EV', 'PR', 'WM');");

            migrationBuilder.Sql(
                "UPDATE `hub_user_grants` SET `department` = CASE `department` WHEN 'SO' THEN 'SOD' WHEN 'FO' THEN 'FOD' WHEN 'AO' THEN 'AOD' WHEN 'TR' THEN 'TD' WHEN 'MB' THEN 'MD' WHEN 'EV' THEN 'ED' WHEN 'PR' THEN 'PRD' WHEN 'WM' THEN 'WD' ELSE `department` END WHERE `department` IN ('SO', 'FO', 'AO', 'TR', 'MB', 'EV', 'PR', 'WM');");

            migrationBuilder.Sql(
                "UPDATE `cms_links` SET `owner_department` = CASE `owner_department` WHEN 'SO' THEN 'SOD' WHEN 'FO' THEN 'FOD' WHEN 'AO' THEN 'AOD' WHEN 'TR' THEN 'TD' WHEN 'MB' THEN 'MD' WHEN 'EV' THEN 'ED' WHEN 'PR' THEN 'PRD' WHEN 'WM' THEN 'WD' ELSE `owner_department` END WHERE `owner_department` IN ('SO', 'FO', 'AO', 'TR', 'MB', 'EV', 'PR', 'WM');");

            migrationBuilder.Sql(
                "UPDATE `cms_contents` SET `owner_department` = CASE `owner_department` WHEN 'SO' THEN 'SOD' WHEN 'FO' THEN 'FOD' WHEN 'AO' THEN 'AOD' WHEN 'TR' THEN 'TD' WHEN 'MB' THEN 'MD' WHEN 'EV' THEN 'ED' WHEN 'PR' THEN 'PRD' WHEN 'WM' THEN 'WD' ELSE `owner_department` END WHERE `owner_department` IN ('SO', 'FO', 'AO', 'TR', 'MB', 'EV', 'PR', 'WM');");

            migrationBuilder.Sql(
                "UPDATE `cms_search_index` SET `owner_department` = CASE `owner_department` WHEN 'SO' THEN 'SOD' WHEN 'FO' THEN 'FOD' WHEN 'AO' THEN 'AOD' WHEN 'TR' THEN 'TD' WHEN 'MB' THEN 'MD' WHEN 'EV' THEN 'ED' WHEN 'PR' THEN 'PRD' WHEN 'WM' THEN 'WD' ELSE `owner_department` END WHERE `owner_department` IN ('SO', 'FO', 'AO', 'TR', 'MB', 'EV', 'PR', 'WM');");

            migrationBuilder.Sql(
                "UPDATE `cms_calendar_entries` SET `owner_department` = CASE `owner_department` WHEN 'SO' THEN 'SOD' WHEN 'FO' THEN 'FOD' WHEN 'AO' THEN 'AOD' WHEN 'TR' THEN 'TD' WHEN 'MB' THEN 'MD' WHEN 'EV' THEN 'ED' WHEN 'PR' THEN 'PRD' WHEN 'WM' THEN 'WD' ELSE `owner_department` END WHERE `owner_department` IN ('SO', 'FO', 'AO', 'TR', 'MB', 'EV', 'PR', 'WM');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

            // Back to the old codes before the columns are narrowed again, or the values would no
            // longer fit.
            migrationBuilder.Sql(
                "UPDATE `hub_user_staff_positions` SET `department` = CASE `department` WHEN 'SOD' THEN 'SO' WHEN 'FOD' THEN 'FO' WHEN 'AOD' THEN 'AO' WHEN 'TD' THEN 'TR' WHEN 'MD' THEN 'MB' WHEN 'ED' THEN 'EV' WHEN 'PRD' THEN 'PR' WHEN 'WD' THEN 'WM' ELSE `department` END WHERE `department` IN ('SOD', 'FOD', 'AOD', 'TD', 'MD', 'ED', 'PRD', 'WD');");

            migrationBuilder.Sql(
                "UPDATE `hub_user_grants` SET `department` = CASE `department` WHEN 'SOD' THEN 'SO' WHEN 'FOD' THEN 'FO' WHEN 'AOD' THEN 'AO' WHEN 'TD' THEN 'TR' WHEN 'MD' THEN 'MB' WHEN 'ED' THEN 'EV' WHEN 'PRD' THEN 'PR' WHEN 'WD' THEN 'WM' ELSE `department` END WHERE `department` IN ('SOD', 'FOD', 'AOD', 'TD', 'MD', 'ED', 'PRD', 'WD');");

            migrationBuilder.Sql(
                "UPDATE `cms_links` SET `owner_department` = CASE `owner_department` WHEN 'SOD' THEN 'SO' WHEN 'FOD' THEN 'FO' WHEN 'AOD' THEN 'AO' WHEN 'TD' THEN 'TR' WHEN 'MD' THEN 'MB' WHEN 'ED' THEN 'EV' WHEN 'PRD' THEN 'PR' WHEN 'WD' THEN 'WM' ELSE `owner_department` END WHERE `owner_department` IN ('SOD', 'FOD', 'AOD', 'TD', 'MD', 'ED', 'PRD', 'WD');");

            migrationBuilder.Sql(
                "UPDATE `cms_contents` SET `owner_department` = CASE `owner_department` WHEN 'SOD' THEN 'SO' WHEN 'FOD' THEN 'FO' WHEN 'AOD' THEN 'AO' WHEN 'TD' THEN 'TR' WHEN 'MD' THEN 'MB' WHEN 'ED' THEN 'EV' WHEN 'PRD' THEN 'PR' WHEN 'WD' THEN 'WM' ELSE `owner_department` END WHERE `owner_department` IN ('SOD', 'FOD', 'AOD', 'TD', 'MD', 'ED', 'PRD', 'WD');");

            migrationBuilder.Sql(
                "UPDATE `cms_search_index` SET `owner_department` = CASE `owner_department` WHEN 'SOD' THEN 'SO' WHEN 'FOD' THEN 'FO' WHEN 'AOD' THEN 'AO' WHEN 'TD' THEN 'TR' WHEN 'MD' THEN 'MB' WHEN 'ED' THEN 'EV' WHEN 'PRD' THEN 'PR' WHEN 'WD' THEN 'WM' ELSE `owner_department` END WHERE `owner_department` IN ('SOD', 'FOD', 'AOD', 'TD', 'MD', 'ED', 'PRD', 'WD');");

            migrationBuilder.Sql(
                "UPDATE `cms_calendar_entries` SET `owner_department` = CASE `owner_department` WHEN 'SOD' THEN 'SO' WHEN 'FOD' THEN 'FO' WHEN 'AOD' THEN 'AO' WHEN 'TD' THEN 'TR' WHEN 'MD' THEN 'MB' WHEN 'ED' THEN 'EV' WHEN 'PRD' THEN 'PR' WHEN 'WD' THEN 'WM' ELSE `owner_department` END WHERE `owner_department` IN ('SOD', 'FOD', 'AOD', 'TD', 'MD', 'ED', 'PRD', 'WD');");

            migrationBuilder.AlterColumn<string>(
                name: "department",
                table: "hub_user_staff_positions",
                type: "varchar(2)",
                maxLength: 2,
                nullable: true,
                collation: "utf8mb4_unicode_ci",
                oldClrType: typeof(string),
                oldType: "varchar(4)",
                oldMaxLength: 4,
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("Relational:Collation", "utf8mb4_unicode_ci");

            migrationBuilder.AlterColumn<string>(
                name: "department",
                table: "hub_user_grants",
                type: "varchar(2)",
                maxLength: 2,
                nullable: true,
                collation: "utf8mb4_unicode_ci",
                oldClrType: typeof(string),
                oldType: "varchar(4)",
                oldMaxLength: 4,
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("Relational:Collation", "utf8mb4_unicode_ci");

            migrationBuilder.AlterColumn<string>(
                name: "owner_department",
                table: "cms_search_index",
                type: "varchar(2)",
                maxLength: 2,
                nullable: false,
                collation: "utf8mb4_unicode_ci",
                oldClrType: typeof(string),
                oldType: "varchar(4)",
                oldMaxLength: 4)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("Relational:Collation", "utf8mb4_unicode_ci");

            migrationBuilder.AlterColumn<string>(
                name: "owner_department",
                table: "cms_links",
                type: "varchar(2)",
                maxLength: 2,
                nullable: false,
                collation: "utf8mb4_unicode_ci",
                oldClrType: typeof(string),
                oldType: "varchar(4)",
                oldMaxLength: 4)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("Relational:Collation", "utf8mb4_unicode_ci");

            migrationBuilder.AlterColumn<string>(
                name: "owner_department",
                table: "cms_contents",
                type: "varchar(2)",
                maxLength: 2,
                nullable: false,
                collation: "utf8mb4_unicode_ci",
                oldClrType: typeof(string),
                oldType: "varchar(4)",
                oldMaxLength: 4)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("Relational:Collation", "utf8mb4_unicode_ci");

            migrationBuilder.AlterColumn<string>(
                name: "owner_department",
                table: "cms_calendar_entries",
                type: "varchar(2)",
                maxLength: 2,
                nullable: false,
                collation: "utf8mb4_unicode_ci",
                oldClrType: typeof(string),
                oldType: "varchar(4)",
                oldMaxLength: 4)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("Relational:Collation", "utf8mb4_unicode_ci");
        }
    }
}
