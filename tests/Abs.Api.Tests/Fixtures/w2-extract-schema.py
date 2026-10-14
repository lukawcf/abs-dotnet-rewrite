# Reads only DDL from the isolated archive copy, never a live Node database.
import sqlite3
from pathlib import Path
db = sqlite3.connect("file:D:/Waikato/26b/COMPX574/tmp/abs-d-seed.sqlite?mode=ro", uri=True)
ddl = db.execute("SELECT sql FROM sqlite_master WHERE name = ?", ("mediaProgresses",)).fetchone()[0]
Path(__file__).with_name("w2-progress-schema.sql").write_text(ddl + ";\n", encoding="utf-8")
tables = ("libraryItems", "books", "podcasts", "podcastEpisodes")
media = [db.execute("SELECT sql FROM sqlite_master WHERE name = ?", (name,)).fetchone()[0] for name in tables]
Path(__file__).with_name("w2-write-media-schema.sql").write_text(";\n".join(media) + ";\n", encoding="utf-8")
