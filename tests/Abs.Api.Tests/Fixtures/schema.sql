-- Table definitions copied from a real audiobookshelf 2.37.1 database (sqlite_master).
-- Test setup only. Production code never creates or changes the schema.
CREATE TABLE `users` (`id` UUID PRIMARY KEY, `username` VARCHAR(255), `email` VARCHAR(255), `pash` VARCHAR(255), `type` VARCHAR(255), `token` VARCHAR(255), `isActive` TINYINT(1) DEFAULT 0, `isLocked` TINYINT(1) DEFAULT 0, `lastSeen` DATETIME, `permissions` JSON, `bookmarks` JSON, `extraData` JSON, `createdAt` DATETIME NOT NULL, `updatedAt` DATETIME NOT NULL);
CREATE TABLE `libraries` (`id` UUID PRIMARY KEY, `name` VARCHAR(255), `displayOrder` INTEGER, `icon` VARCHAR(255), `mediaType` VARCHAR(255), `provider` VARCHAR(255), `lastScan` DATETIME, `lastScanVersion` VARCHAR(255), `settings` JSON, `extraData` JSON, `createdAt` DATETIME NOT NULL, `updatedAt` DATETIME NOT NULL);
CREATE TABLE `libraryFolders` (`id` UUID PRIMARY KEY, `path` VARCHAR(255), `createdAt` DATETIME NOT NULL, `updatedAt` DATETIME NOT NULL, `libraryId` UUID REFERENCES `libraries` (`id`) ON DELETE CASCADE ON UPDATE CASCADE);

-- Library Items tables: DDL from audiobookshelf 2.37.1 models (tests/tools/record-node-items.js, Sequelize sync on in-memory SQLite).
CREATE TABLE `settings` (`key` VARCHAR(255) PRIMARY KEY, `value` JSON, `createdAt` DATETIME NOT NULL, `updatedAt` DATETIME NOT NULL);
CREATE TABLE `authors` (`id` UUID PRIMARY KEY, `name` VARCHAR(255), `lastFirst` VARCHAR(255), `asin` VARCHAR(255), `description` TEXT, `imagePath` VARCHAR(255), `createdAt` DATETIME NOT NULL, `updatedAt` DATETIME NOT NULL, `libraryId` UUID REFERENCES `libraries` (`id`) ON DELETE CASCADE ON UPDATE CASCADE);
CREATE TABLE `series` (`id` UUID PRIMARY KEY, `name` VARCHAR(255), `nameIgnorePrefix` VARCHAR(255), `description` TEXT, `createdAt` DATETIME NOT NULL, `updatedAt` DATETIME NOT NULL, `libraryId` UUID REFERENCES `libraries` (`id`) ON DELETE CASCADE ON UPDATE CASCADE);
CREATE TABLE `books` (`id` UUID PRIMARY KEY, `title` VARCHAR(255), `titleIgnorePrefix` VARCHAR(255), `subtitle` VARCHAR(255), `publishedYear` VARCHAR(255), `publishedDate` VARCHAR(255), `publisher` VARCHAR(255), `description` TEXT, `isbn` VARCHAR(255), `asin` VARCHAR(255), `language` VARCHAR(255), `explicit` TINYINT(1), `abridged` TINYINT(1), `coverPath` VARCHAR(255), `duration` FLOAT, `narrators` JSON, `audioFiles` JSON, `ebookFile` JSON, `chapters` JSON, `tags` JSON, `genres` JSON, `createdAt` DATETIME NOT NULL, `updatedAt` DATETIME NOT NULL);
CREATE TABLE `libraryItems` (`id` UUID PRIMARY KEY, `ino` VARCHAR(255), `path` VARCHAR(255), `relPath` VARCHAR(255), `mediaId` UUID, `mediaType` VARCHAR(255), `isFile` TINYINT(1), `isMissing` TINYINT(1), `isInvalid` TINYINT(1), `mtime` DATETIME, `ctime` DATETIME, `birthtime` DATETIME, `size` BIGINT, `lastScan` DATETIME, `lastScanVersion` VARCHAR(255), `libraryFiles` JSON, `extraData` JSON, `title` VARCHAR(255), `titleIgnorePrefix` VARCHAR(255), `authorNamesFirstLast` VARCHAR(255), `authorNamesLastFirst` VARCHAR(255), `createdAt` DATETIME NOT NULL, `updatedAt` DATETIME NOT NULL, `libraryId` UUID REFERENCES `libraries` (`id`) ON DELETE SET NULL ON UPDATE CASCADE, `libraryFolderId` UUID REFERENCES `libraryFolders` (`id`) ON DELETE SET NULL ON UPDATE CASCADE);
CREATE TABLE `bookAuthors` (`id` UUID PRIMARY KEY, `createdAt` DATETIME NOT NULL, `bookId` UUID REFERENCES `books` (`id`) ON DELETE CASCADE ON UPDATE CASCADE, `authorId` UUID REFERENCES `authors` (`id`) ON DELETE CASCADE ON UPDATE CASCADE, UNIQUE (`bookId`, `authorId`));
CREATE TABLE `bookSeries` (`id` UUID PRIMARY KEY, `sequence` VARCHAR(255), `createdAt` DATETIME NOT NULL, `bookId` UUID REFERENCES `books` (`id`) ON DELETE CASCADE ON UPDATE CASCADE, `seriesId` UUID REFERENCES `series` (`id`) ON DELETE CASCADE ON UPDATE CASCADE, UNIQUE (`bookId`, `seriesId`));
CREATE INDEX `books_title` ON `books` (`title` COLLATE `NOCASE`);
CREATE INDEX `books_published_year` ON `books` (`publishedYear`);
CREATE INDEX `books_duration` ON `books` (`duration`);
CREATE INDEX `library_items_created_at` ON `libraryItems` (`createdAt`);
CREATE INDEX `library_items_media_id` ON `libraryItems` (`mediaId`);
CREATE INDEX `library_items_library_id_media_type` ON `libraryItems` (`libraryId`, `mediaType`);
CREATE INDEX `library_items_library_id_media_type_size` ON `libraryItems` (`libraryId`, `mediaType`, `size`);
CREATE INDEX `library_items_library_id_media_type_created_at` ON `libraryItems` (`libraryId`, `mediaType`, `createdAt`);
CREATE INDEX `library_items_library_id_media_type_title` ON `libraryItems` (`libraryId`, `mediaType`, `title` COLLATE `NOCASE`);
CREATE INDEX `library_items_library_id_media_type_title_ignore_prefix` ON `libraryItems` (`libraryId`, `mediaType`, `titleIgnorePrefix` COLLATE `NOCASE`);
CREATE INDEX `library_items_library_id_media_type_author_names_first_last` ON `libraryItems` (`libraryId`, `mediaType`, `authorNamesFirstLast` COLLATE `NOCASE`);
CREATE INDEX `library_items_library_id_media_type_author_names_last_first` ON `libraryItems` (`libraryId`, `mediaType`, `authorNamesLastFirst` COLLATE `NOCASE`);
CREATE INDEX `library_items_library_id_media_id_media_type` ON `libraryItems` (`libraryId`, `mediaId`, `mediaType`);
CREATE INDEX `library_items_birthtime` ON `libraryItems` (`birthtime`);
CREATE INDEX `library_items_mtime` ON `libraryItems` (`mtime`);
CREATE INDEX `series_name` ON `series` (`name` COLLATE `NOCASE`);
CREATE UNIQUE INDEX `unique_series_name_per_library` ON `series` (`name`, `libraryId`);
CREATE INDEX `series_library_id` ON `series` (`libraryId`);
CREATE INDEX `bookSeries_seriesId` ON `bookSeries` (`seriesId`);
CREATE INDEX `book_series_series_book` ON `bookSeries` (`seriesId`, `bookId`);
CREATE INDEX `authors_name` ON `authors` (`name` COLLATE `NOCASE`);
CREATE INDEX `authors_library_id` ON `authors` (`libraryId`);
CREATE INDEX `bookAuthor_authorId` ON `bookAuthors` (`authorId`);
