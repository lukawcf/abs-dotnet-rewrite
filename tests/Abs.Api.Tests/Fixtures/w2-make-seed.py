import json
from pathlib import Path
p = Path(__file__).parent
rows = json.loads((p / "w2-progress-input.json").read_text())
def literal(v):
    if v is None: return "NULL"
    if isinstance(v, bool): return str(int(v))
    if isinstance(v, (int, float)): return str(v)
    if isinstance(v, dict): v = json.dumps(v, separators=(",", ":"))
    return "'" + v.replace("'", "''") + "'"
sql = []
for user in ("w2-alice", "w2-bob", "w2-empty"):
    sql.append("INSERT INTO users(id,createdAt,updatedAt) VALUES(" + literal(user) + ",'2026-10-01 00:00:00.000 +00:00','2026-10-01 00:00:00.000 +00:00');")
for row in rows:
    for key in ("createdAt", "updatedAt", "finishedAt"):
        if row[key]: row[key] = row[key].replace("T", " ").replace("Z", " +00:00")
    sql.append("INSERT INTO mediaProgresses(" + ",".join(row) + ") VALUES(" + ",".join(literal(v) for v in row.values()) + ");")
(p / "w2-progress-seed.sql").write_text("\n".join(sql) + "\n", encoding="utf-8")
