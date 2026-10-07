-- Library A is a verbatim copy of the row that Node returned in node-libraries.json.
INSERT INTO libraries VALUES ('aeb8a59a-eb09-4444-bd9d-2ca5cf8fa161', 'Test Book', 1, 'database', 'book', 'google', '2026-10-06 22:28:55.368 +00:00', '2.37.1', '{"coverAspectRatio":1,"disableWatcher":false,"autoScanCronExpression":null,"skipMatchingMediaWithAsin":false,"skipMatchingMediaWithIsbn":false,"audiobooksOnly":false,"epubsAllowScriptedContent":false,"hideSingleBookSeries":false,"onlyShowLaterBooksInContinueSeries":false,"metadataPrecedence":["folderStructure","audioMetatags","nfoFile","txtFiles","opfFile","absMetadata"],"markAsFinishedPercentComplete":null,"markAsFinishedTimeRemaining":10}', '{"lastScanMetadataPrecedence":["folderStructure","audioMetatags","nfoFile","txtFiles","opfFile","absMetadata"]}', '2026-10-06 22:08:12.334 +00:00', '2026-10-06 22:28:55.368 +00:00');
INSERT INTO libraryFolders VALUES ('72033519-7e80-4bb8-bc72-7895b05568bc', '/audiobooks', '2026-10-06 22:08:12.376 +00:00', '2026-10-06 22:08:12.376 +00:00', 'aeb8a59a-eb09-4444-bd9d-2ca5cf8fa161');

-- Library B: never scanned, no settings, no folders, shown second.
INSERT INTO libraries VALUES ('bbbbbbbb-0000-4000-8000-000000000002', 'Podcasts', 2, 'podcast', 'podcast', NULL, NULL, NULL, NULL, NULL, '2026-10-07 01:00:00.000 +00:00', '2026-10-07 01:00:00.000 +00:00');

-- admin: all libraries.  only-a: restricted to A.  only-b: restricted to B.
-- quirk: accessAllLibraries true AND a non-empty list (the list endpoint filters by the list, the single endpoint does not).
-- no-perms: user row without permissions.
INSERT INTO users (id, username, type, isActive, permissions, createdAt, updatedAt) VALUES
 ('user-admin', 'admin', 'admin', 1, '{"accessAllLibraries":true,"librariesAccessible":[]}', '2026-10-06 22:08:12.000 +00:00', '2026-10-06 22:08:12.000 +00:00'),
 ('user-only-a', 'only-a', 'user', 1, '{"accessAllLibraries":false,"librariesAccessible":["aeb8a59a-eb09-4444-bd9d-2ca5cf8fa161"]}', '2026-10-06 22:08:12.000 +00:00', '2026-10-06 22:08:12.000 +00:00'),
 ('user-only-b', 'only-b', 'user', 1, '{"accessAllLibraries":false,"librariesAccessible":["bbbbbbbb-0000-4000-8000-000000000002"]}', '2026-10-06 22:08:12.000 +00:00', '2026-10-06 22:08:12.000 +00:00'),
 ('user-quirk', 'quirk', 'user', 1, '{"accessAllLibraries":true,"librariesAccessible":["bbbbbbbb-0000-4000-8000-000000000002"]}', '2026-10-06 22:08:12.000 +00:00', '2026-10-06 22:08:12.000 +00:00'),
 ('user-no-perms', 'no-perms', 'user', 1, NULL, '2026-10-06 22:08:12.000 +00:00', '2026-10-06 22:08:12.000 +00:00');
