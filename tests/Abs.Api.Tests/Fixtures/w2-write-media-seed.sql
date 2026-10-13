INSERT INTO books(id,createdAt,updatedAt) VALUES
('w2-book','2026-10-01 00:00:00.000 +00:00','2026-10-01 00:00:00.000 +00:00'),
('w2-new-book','2026-10-01 00:00:00.000 +00:00','2026-10-01 00:00:00.000 +00:00'),
('w2-bob-book','2026-10-01 00:00:00.000 +00:00','2026-10-01 00:00:00.000 +00:00');
INSERT INTO libraryItems(id,mediaId,mediaType,createdAt,updatedAt) VALUES
('w2-item','w2-book','book','2026-10-01 00:00:00.000 +00:00','2026-10-01 00:00:00.000 +00:00'),
('w2-new-item','w2-new-book','book','2026-10-01 00:00:00.000 +00:00','2026-10-01 00:00:00.000 +00:00'),
('w2-private-item','w2-bob-book','book','2026-10-01 00:00:00.000 +00:00','2026-10-01 00:00:00.000 +00:00'),
('w2-podcast','w2-podcast-media','podcast','2026-10-01 00:00:00.000 +00:00','2026-10-01 00:00:00.000 +00:00');
INSERT INTO podcasts(id,createdAt,updatedAt) VALUES('w2-podcast-media','2026-10-01 00:00:00.000 +00:00','2026-10-01 00:00:00.000 +00:00');
INSERT INTO podcastEpisodes(id,podcastId,createdAt,updatedAt) VALUES
('w2-episode','w2-podcast-media','2026-10-01 00:00:00.000 +00:00','2026-10-01 00:00:00.000 +00:00'),
('w2-new-episode','w2-podcast-media','2026-10-01 00:00:00.000 +00:00','2026-10-01 00:00:00.000 +00:00');
