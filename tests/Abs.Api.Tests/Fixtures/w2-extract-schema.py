# Reads only DDL from the isolated archive copy, never a live Node database.
import sqlite3
from pathlib import Path
db = sqlite3.connect("file:D:/Waikato/26b/COMPX574/tmp/abs-d-seed.sqlite?mode=ro", uri=True)
ddl = db.execute("SELECT sql FROM sqlite_master WHERE name = ?", ("mediaProgresses",)).fetchone()[0]
Path(__file__).with_name("w2-progress-schema.sql").write_text(ddl + ";\n", encoding="utf-8")
