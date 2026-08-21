/*
    Yunu.Commerce - Canonical Taxonomy Concurrency Guard
    Target: SQL Server 2022+

    Closes the final Canonical Taxonomy Foundation Freeze guard (docs task:
    "Yunu.Commerce - Canonical Taxonomy Concurrency Guard"): mutations to
    Catalog.CanonicalTaxonomyNodes must follow first-writer-wins optimistic
    concurrency. Revision is a purely technical persistence/concurrency
    token: it is never surfaced as a CanonicalTaxonomyNode business
    invariant and never exposed through public read contracts.

    Every successful mutation (rename/update, lifecycle transition, and a
    child being created under a parent) increments Revision by exactly 1.
    Catalog.Infrastructure conditions its writes on the caller-supplied
    expected Revision; zero affected rows surfaces an explicit concurrency
    conflict instead of silently overwriting newer persisted state.

    The script is idempotent: it only adds the column (with a default so it
    is safe against existing rows) when it does not already exist.
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'Catalog.CanonicalTaxonomyNodes', N'U') IS NULL
        THROW 51070, 'Catalog.CanonicalTaxonomyNodes does not exist.', 1;

    IF COL_LENGTH(N'Catalog.CanonicalTaxonomyNodes', N'Revision') IS NULL
    BEGIN
        ALTER TABLE Catalog.CanonicalTaxonomyNodes
            ADD Revision BIGINT NOT NULL
                CONSTRAINT DF_CanonicalTaxonomyNodes_Revision DEFAULT (1);
    END;

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.check_constraints
        WHERE parent_object_id = OBJECT_ID(N'Catalog.CanonicalTaxonomyNodes')
          AND name = N'CK_CanonicalTaxonomyNodes_Revision'
    )
    BEGIN
        /*
            Dynamic SQL avoids SQL Server batch binding failures when the
            column is created and referenced in the same migration batch.
        */
        EXEC sys.sp_executesql N'
            ALTER TABLE Catalog.CanonicalTaxonomyNodes
                ADD CONSTRAINT CK_CanonicalTaxonomyNodes_Revision
                    CHECK (Revision > 0);';
    END;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    THROW;
END CATCH;
