using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DiagnosticLabs.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Annual Physical Exam Page 2 (module 17) becomes the second tab of the Annual Physical Exam (module 16): one result, printed on two pages.
    /// No column changes; the data moves:
    /// <list type="bullet">
    /// <item>a saved Page 2 joins an Annual PE of the same registration that has no Page 2 yet (its row moves there and the emptied result is
    /// deleted); any other Page 2 becomes an Annual PE itself, with an empty first page;</item>
    /// <item>Page 2's entry lists and saved defaults move to the Annual PE (a default the Annual PE already has wins);</item>
    /// <item>the Page 2 service is deleted: a registration or package line of it becomes an Annual PE line; when that registration or package
    /// already has the Annual PE the line is dropped, and on a registration its price is added to the Annual PE line so the total stays as billed;
    /// module 17 is hidden.</item>
    /// </list>
    /// The same SQL runs at the end of <c>db/migrate-from-legacy.sql</c> (the client's data is imported after the migrations).
    /// </summary>
    public partial class MergeAnnualPhysicalExamPage2 : Migration
    {
        internal const string MergeSql = """
            -- 1. Saved Page 2 results become Page 2 of an Annual Physical Exam
            DECLARE @pairs TABLE (Page2Id bigint PRIMARY KEY, ApeId bigint NOT NULL UNIQUE);
            WITH p AS (
                SELECT Id, PatientRegistrationId, ROW_NUMBER() OVER (PARTITION BY PatientRegistrationId ORDER BY Id) AS n
                FROM LabReports WHERE ReportType = N'MedicalExamination' AND IsDeleted = 0 AND PatientRegistrationId IS NOT NULL),
            a AS (
                SELECT r.Id, r.PatientRegistrationId, ROW_NUMBER() OVER (PARTITION BY r.PatientRegistrationId ORDER BY r.Id) AS n
                FROM LabReports r
                WHERE r.ReportType = N'AnnualPhysicalExam' AND r.IsDeleted = 0 AND r.PatientRegistrationId IS NOT NULL
                  AND NOT EXISTS (SELECT 1 FROM MedicalExaminationReports x WHERE x.LabReportId = r.Id))
            INSERT INTO @pairs (Page2Id, ApeId)
            SELECT p.Id, a.Id FROM p JOIN a ON a.PatientRegistrationId = p.PatientRegistrationId AND a.n = p.n;

            -- a Page 2 of a registration that has an Annual PE: its row moves there, with its Remarks, contact number and civil status where the Annual PE has none
            UPDATE x SET LabReportId = pr.ApeId FROM MedicalExaminationReports x JOIN @pairs pr ON pr.Page2Id = x.LabReportId;
            UPDATE a SET Remarks = COALESCE(a.Remarks, m.Remarks)
            FROM LabReports a JOIN @pairs pr ON pr.ApeId = a.Id JOIN LabReports m ON m.Id = pr.Page2Id;
            UPDATE e SET ContactNo = COALESCE(e.ContactNo, x.ContactNo), CivilStatus = COALESCE(e.CivilStatus, x.CivilStatus)
            FROM AnnualPhysicalExamReports e JOIN @pairs pr ON pr.ApeId = e.LabReportId JOIN MedicalExaminationReports x ON x.LabReportId = e.LabReportId;
            UPDATE LabReports SET IsDeleted = 1, DeletedAtUtc = SYSUTCDATETIME() WHERE Id IN (SELECT Page2Id FROM @pairs);

            -- any other Page 2 becomes an Annual PE itself, with an empty first page that takes its contact number and civil status
            INSERT INTO AnnualPhysicalExamReports (LabReportId, ContactNo, CivilStatus, LMPType, VitalSignsBy, HeightWeightBy)
            SELECT m.Id, x.ContactNo, x.CivilStatus, N'', N'', N''
            FROM LabReports m LEFT JOIN MedicalExaminationReports x ON x.LabReportId = m.Id
            WHERE m.ReportType = N'MedicalExamination' AND m.Id NOT IN (SELECT Page2Id FROM @pairs)
              AND NOT EXISTS (SELECT 1 FROM AnnualPhysicalExamReports e WHERE e.LabReportId = m.Id);
            UPDATE LabReports SET ReportType = N'AnnualPhysicalExam'
            WHERE ReportType = N'MedicalExamination' AND Id NOT IN (SELECT Page2Id FROM @pairs);

            -- 2. Page 2's entry lists belong to the Annual PE now
            UPDATE v SET ModuleId = 16 FROM LookupValues v
            WHERE v.ModuleId = 17 AND NOT EXISTS (SELECT 1 FROM LookupValues o WHERE o.ModuleId = 16 AND o.FieldName = v.FieldName);

            -- 3. One Defaults: Page 2's saved defaults join the Annual PE's (a value the Annual PE already has wins)
            IF EXISTS (SELECT 1 FROM ModuleDefaults WHERE ModuleId = 17)
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM ModuleDefaults WHERE ModuleId = 16)
                    UPDATE ModuleDefaults SET ModuleId = 16 WHERE ModuleId = 17;
                ELSE
                BEGIN
                    DECLARE @merged nvarchar(max) = (SELECT Defaults FROM ModuleDefaults WHERE ModuleId = 16);
                    DECLARE @keys TABLE (n int IDENTITY PRIMARY KEY, [key] nvarchar(4000), value nvarchar(max));
                    INSERT INTO @keys ([key], value)
                    SELECT [key], value FROM OPENJSON((SELECT Defaults FROM ModuleDefaults WHERE ModuleId = 17)) WHERE type = 1;
                    DECLARE @i int = 1, @last int = (SELECT COUNT(*) FROM @keys), @key nvarchar(4000), @value nvarchar(max);
                    WHILE @i <= @last
                    BEGIN
                        SELECT @key = [key], @value = value FROM @keys WHERE n = @i;
                        IF JSON_VALUE(@merged, N'$."' + @key + N'"') IS NULL
                            SET @merged = JSON_MODIFY(@merged, N'$."' + @key + N'"', @value);
                        SET @i += 1;
                    END;
                    UPDATE ModuleDefaults SET Defaults = @merged, UpdatedAtUtc = SYSUTCDATETIME() WHERE ModuleId = 16;
                    DELETE FROM ModuleDefaults WHERE ModuleId = 17;
                END;
            END;

            -- 4. The Page 2 service is deleted and its module hidden
            DECLARE @page2Service bigint = (SELECT ServiceId FROM Modules WHERE Id = 17);
            DECLARE @apeService bigint = (SELECT ServiceId FROM Modules WHERE Id = 16);
            UPDATE Modules SET ServiceId = NULL, IsActive = 0 WHERE Id = 17;
            -- (without an Annual PE service there is nothing to move the lines to: the Page 2 service is then left as it is, unlinked)
            IF @page2Service IS NOT NULL AND @apeService IS NOT NULL
            BEGIN
                -- a registration billed for both keeps its total: the Page 2 price is added to its Annual PE line, so the lines still add up to the amount due
                UPDATE a SET Price = a.Price + x.Total, UpdatedAtUtc = SYSUTCDATETIME()
                FROM PatientRegistrationServices a
                JOIN (SELECT p.PatientRegistrationId, SUM(p.Price) AS Total,
                             (SELECT MIN(o.Id) FROM PatientRegistrationServices o
                              WHERE o.PatientRegistrationId = p.PatientRegistrationId AND o.ServiceId = @apeService AND o.IsDeleted = 0) AS ApeLineId
                      FROM PatientRegistrationServices p
                      WHERE p.ServiceId = @page2Service AND p.IsDeleted = 0
                      GROUP BY p.PatientRegistrationId) x ON x.ApeLineId = a.Id;
                DELETE s FROM PatientRegistrationServices s
                WHERE s.ServiceId = @page2Service
                  AND EXISTS (SELECT 1 FROM PatientRegistrationServices o WHERE o.PatientRegistrationId = s.PatientRegistrationId AND o.ServiceId = @apeService AND o.IsDeleted = 0);
                UPDATE PatientRegistrationServices SET ServiceId = @apeService WHERE ServiceId = @page2Service;

                -- a package lists services but is priced as a whole, so dropping a line does not change what it costs
                DELETE s FROM PackageServices s
                WHERE s.ServiceId = @page2Service
                  AND EXISTS (SELECT 1 FROM PackageServices o WHERE o.PackageId = s.PackageId AND o.ServiceId = @apeService);
                UPDATE PackageServices SET ServiceId = @apeService WHERE ServiceId = @page2Service;

                DELETE FROM ServiceItemQuantities WHERE ServiceId = @page2Service;
                IF NOT EXISTS (SELECT 1 FROM Modules WHERE ServiceId = @page2Service)
                    DELETE FROM Services WHERE Id = @page2Service;
            END;
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(MergeSql);
        }

        /// <summary>
        /// Puts module 17 back with a service of its own (price 0, a new id) and its entry lists. The merged results stay merged: a Page 2 saved
        /// with an Annual PE can not be told apart from one that was always there, so the old Page 2 screen will not list them.
        /// </summary>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM Modules WHERE Id = 17)
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM Services WHERE ServiceName = N'Annual Physical Exam Page 2')
                        INSERT INTO Services (ServiceName, ServiceDescription, Price, CreatedAtUtc, UpdatedAtUtc, IsActive)
                        VALUES (N'Annual Physical Exam Page 2', N'Annual Physical Exam Page 2', 0, SYSUTCDATETIME(), SYSUTCDATETIME(), 1);
                    UPDATE Modules SET IsActive = 1, ServiceId = (SELECT TOP 1 Id FROM Services WHERE ServiceName = N'Annual Physical Exam Page 2' ORDER BY Id)
                    WHERE Id = 17;
                    UPDATE LookupValues SET ModuleId = 17
                    WHERE ModuleId = 16 AND FieldName IN (N'Assessment Done By', N'Physician Name', N'Physician License', N'Medical/Surgical History', N'Assessment', N'Remarks');
                END;
                """);
        }
    }
}
