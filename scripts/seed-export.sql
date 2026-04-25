-- Prunes a freshly-restored prod export down to public-only seed data.
--
-- Run against the temp export DB (the wrapper script sets the database
-- context via sqlcmd -d). Cascade FKs from user-scoped tables to dbo.[User]
-- are intact in the schema, so deleting non-public users purges their
-- scores, sessions, preferences, etc. automatically.
--
-- Output (PRINT statements) is captured by the wrapper for the run summary.

SET NOCOUNT ON;

DECLARE @beforeUsers   INT    = (SELECT COUNT(*) FROM dbo.[User]);
DECLARE @publicUsers   INT    = (SELECT COUNT(*) FROM dbo.[User] WHERE IsPublic = 1);
DECLARE @beforeScores  BIGINT = (SELECT COUNT(*) FROM dbo.PhoenixBestAttempt);

PRINT CONCAT('Users before:    ', @beforeUsers, ' (', @publicUsers, ' public)');
PRINT CONCAT('Scores before:   ', @beforeScores);

-- Auth/credential tables: wipe outright. These should never ship in a
-- public .bak even for users who opted into public visibility.
DELETE FROM dbo.UserApiToken;
DELETE FROM dbo.ExternalLogin;
PRINT 'Cleared dbo.UserApiToken and dbo.ExternalLogin';

-- Drop non-public users; cascade FKs purge their dependent rows.
DELETE FROM dbo.[User] WHERE IsPublic = 0;

DECLARE @afterUsers  INT    = (SELECT COUNT(*) FROM dbo.[User]);
DECLARE @afterScores BIGINT = (SELECT COUNT(*) FROM dbo.PhoenixBestAttempt);

PRINT CONCAT('Users after:     ', @afterUsers);
PRINT CONCAT('Scores after:    ', @afterScores);

-- Reclaim space so the resulting .bak compresses smaller.
DBCC SHRINKDATABASE (0, 1) WITH NO_INFOMSGS;

PRINT 'Prune complete.';
