import json
from pathlib import Path
p=Path(__file__).parent
data=json.loads((p/"w2-bookmark-input.json").read_text())
def quote(v):
    if v is None:return "NULL"
    return "'" + str(v).replace("'","''") + "'"
sql=[]
for u in data["users"]:
    sql.append("INSERT INTO users(id,isActive,permissions,bookmarks,createdAt,updatedAt) VALUES("+",".join([quote(u["id"]),str(int(u["isActive"])),quote(json.dumps(u["permissions"])),quote(json.dumps(u["bookmarks"])) if u["bookmarks"] is not None else "NULL","'2026-10-01 00:00:00.000 +00:00'","'2026-10-01 00:00:00.000 +00:00'"])+");")
for item in data["items"]:
    sql.append("UPDATE libraryItems SET libraryId="+quote(item["libraryId"])+" WHERE id="+quote(item["id"])+";")
    table="books" if item["mediaType"]=="book" else "podcasts"
    sql.append("UPDATE "+table+" SET explicit="+str(int(item["media"]["explicit"]))+",tags="+quote(json.dumps(item["media"]["tags"]))+" WHERE id="+quote(item["mediaId"])+";")
(p/"w2-bookmark-seed.sql").write_text("\n".join(sql)+"\n",encoding="utf-8")
