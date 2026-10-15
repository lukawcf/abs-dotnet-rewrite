"""End-to-end comparison of Node and .NET (the E module).

Starts an isolated stack (compose project "abse2e"): Node 2.37.1 and the .NET service, both on a COPY of an
audiobookshelf database, with a random JWT key made for this run. Sends the same requests to both and
compares status and body. Never touches the live config folder or .env.

    python tests/e2e/run_e2e.py --db G:/Project/abs-scratch/abs-copy.sqlite [--keep]

Exit code 0: every case is identical or an expected, documented difference. 1: at least one unexpected difference.
Needs: docker-compose, Python 3.
"""
import argparse, base64, hashlib, hmac, json, os, secrets, shutil, subprocess, sys, time
import urllib.error, urllib.parse, urllib.request

REPO = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
WORK = os.path.join(os.path.dirname(__file__), ".work")
NODE = "http://localhost:13378"
DOTNET = "http://localhost:18081"
NGINX = "http://localhost:18080"
PROJECT = "abse2e"


def b64(data: bytes) -> str:
    return base64.urlsafe_b64encode(data).rstrip(b"=").decode()


def mint_token(secret: str, user_id: str, username: str) -> str:
    """Same as Node's TokenManager.generateAccessToken: HS256, payload {userId, username}, no expiry."""
    head = b64(json.dumps({"alg": "HS256", "typ": "JWT"}, separators=(",", ":")).encode())
    body = b64(json.dumps({"userId": user_id, "username": username}, separators=(",", ":")).encode())
    sig = hmac.new(secret.encode(), f"{head}.{body}".encode(), hashlib.sha256).digest()
    return f"{head}.{body}.{b64(sig)}"


def call(base, path, token, method="GET", body=None):
    data = json.dumps(body).encode() if body is not None else None
    headers = {"Content-Type": "application/json"}
    if token:
        headers["Authorization"] = "Bearer " + token
    request = urllib.request.Request(base + path, method=method, data=data, headers=headers)
    try:
        response = urllib.request.urlopen(request, timeout=60)
        raw, code = response.read().decode(), response.status
    except urllib.error.HTTPError as error:
        raw, code = error.read().decode(), error.code
    try:
        return code, json.loads(raw)
    except ValueError:
        return code, raw


def compose(*args):
    cmd = ["docker-compose", "-p", PROJECT, "-f", os.path.join(WORK, "compose.yml"), *args]
    return subprocess.run(cmd, capture_output=True, text=True)


def start_stack(db_path, secret):
    shutil.rmtree(WORK, ignore_errors=True)
    for folder in ("config", "metadata"):
        os.makedirs(os.path.join(WORK, folder))
    shutil.copy(db_path, os.path.join(WORK, "config", "absdatabase.sqlite"))
    library = os.path.join(REPO, "library").replace("\\", "/")
    work = WORK.replace("\\", "/")
    repo = REPO.replace("\\", "/")
    with open(os.path.join(WORK, "compose.yml"), "w", encoding="utf-8") as f:
        f.write(f"""services:
  node:
    image: ghcr.io/advplyr/audiobookshelf:latest
    environment:
      - JWT_SECRET_KEY={secret}
    volumes:
      - {library}:/audiobooks:ro
      - {work}/config:/config
      - {work}/metadata:/metadata
    ports: ["13378:80"]
    networks:
      default:
        aliases: [abs-node]
  dotnet:
    build:
      context: {repo}
      dockerfile: src/Abs.Api/Dockerfile
    environment:
      - JWT_SECRET_KEY={secret}
    volumes:
      - {work}/config:/config:ro
    ports: ["18081:8080"]
    networks:
      default:
        aliases: [abs-dotnet]
  nginx:
    image: nginx:stable
    volumes:
      - {repo}/nginx/nginx.conf:/etc/nginx/nginx.conf:ro
    ports: ["18080:80"]
    depends_on: [node, dotnet]
""")
    result = compose("up", "-d", "--build")
    if result.returncode != 0:
        sys.exit("compose up failed:\n" + result.stderr[-2000:])
    for base, path in ((NODE, "/ping"), (DOTNET, "/health"), (NGINX, "/ping")):
        for _ in range(60):
            try:
                urllib.request.urlopen(base + path, timeout=3)
                break
            except Exception:
                time.sleep(1)
        else:
            sys.exit(f"{base}{path} did not come up")


def stop_stack():
    compose("down", "-v", "--remove-orphans")


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--db", required=True, help="a COPY of an audiobookshelf 2.37.x sqlite file")
    parser.add_argument("--keep", action="store_true", help="leave the stack running afterwards")
    args = parser.parse_args()

    secret = secrets.token_hex(32)  # test key for this run only, never written anywhere but the temp compose file
    start_stack(args.db, secret)
    try:
        run(secret)
    finally:
        if not args.keep:
            stop_stack()
            shutil.rmtree(WORK, ignore_errors=True)


def run(secret):
    import sqlite3
    con = sqlite3.connect(f"file:{os.path.join(WORK, 'config', 'absdatabase.sqlite')}?mode=ro", uri=True)
    root_id, root_name = con.execute("select id, username from users where type='root' limit 1").fetchone()
    con.close()
    root = mint_token(secret, root_id, root_name)

    # Extra users, created through Node itself so the rows are real.
    perms_none = {"download": True, "update": False, "delete": False, "upload": False, "createEreader": False,
                  "accessAllLibraries": False, "accessAllTags": True, "accessExplicitContent": False,
                  "selectedTagsNotAccessible": False, "librariesAccessible": [], "itemTagsSelected": []}
    perms_reader = dict(perms_none, accessAllLibraries=True, accessExplicitContent=True)
    users = {}
    for name, perms in (("reader", perms_reader), ("nolib", perms_none)):
        code, body = call(NODE, "/api/users", root, "POST",
                          {"username": name, "password": secrets.token_hex(8), "type": "user", "isActive": True,
                           "permissions": perms})
        assert code == 200, f"cannot create user {name}: {code} {body}"
        users[name] = mint_token(secret, body["user"]["id"], name)
    users["root"] = root

    code, libs = call(NODE, "/api/libraries", root)
    library = libs["libraries"][0]["id"]
    code, listing = call(NODE, f"/api/libraries/{library}/items?sort=addedAt", root)
    items = [r["id"] for r in listing["results"]]

    # Give the read endpoints something to read: progress and bookmarks written by Node itself.
    for who in ("root", "reader"):
        for index, item in enumerate(items[:2]):
            call(NODE, f"/api/me/progress/{item}", users[who], "PATCH",
                 {"duration": 100, "progress": 0.25 * (index + 1), "currentTime": 25 * (index + 1), "isFinished": False})
        call(NODE, f"/api/me/item/{items[0]}/bookmark", users[who], "POST", {"time": 12, "title": "first"})
        call(NODE, f"/api/me/item/{items[0]}/bookmark", users[who], "POST", {"time": 40, "title": "second"})
    root_user_id = root_id

    all_users = ["root", "reader", "nolib"]
    cases = []  # (path, users, expected_difference_or_None)

    def add(path, who=all_users, expected=None):
        cases.append((path, who, expected))

    add("/api/libraries")
    add(f"/api/libraries/{library}")
    add("/api/libraries/does-not-exist")
    add("/api/libraries?include=stats", ["root"], "include=stats stays in Node (501)")
    add(f"/api/libraries/{library}?include=filterdata", ["root"], "include=filterdata stays in Node (501)")
    for item in items:
        add(f"/api/items/{item}")
    add("/api/items/does-not-exist", ["root"])
    add(f"/api/items/{items[0]}?expanded=1", ["root"], "expanded=1 stays in Node (501)")
    for sort in ("addedAt", "size", "birthtimeMs", "mtimeMs", "media.duration", "media.metadata.publishedYear",
                 "media.metadata.authorName", "media.metadata.authorNameLF", "media.metadata.title"):
        for desc in ("0", "1"):
            add(f"/api/libraries/{library}/items?sort={urllib.parse.quote(sort)}&desc={desc}", ["root", "reader"])
    for query in ("limit=2&page=0&sort=size", "limit=2&page=1&sort=size", "limit=2&page=5&sort=size", "limit=0&sort=size",
                  "limit=abc&sort=size", "sort=size&minified=1", "limit=-1"):
        add(f"/api/libraries/{library}/items?{query}", ["root"])
    add(f"/api/libraries/{library}/items?sort=size", ["nolib"])
    add(f"/api/libraries/{library}/items?filter=authors.abc", ["root"], "filter stays in Node (501)")
    add(f"/api/libraries/{library}/items?collapseseries=1", ["root"], "collapseseries stays in Node (501)")
    add("/api/me")
    add("/api/users", ["root", "reader"])
    add(f"/api/users/{root_user_id}", ["root"], "/api/users/:id stays in Node (501): Node adds titles and covers to progress")
    add("/api/users/does-not-exist", ["root"], "/api/users/:id stays in Node (501)")
    add(f"/api/users/{root_user_id}", ["reader"])
    add("/api/users?include=latestSession", ["root"], "include=latestSession stays in Node (501)")
    add("/api/me/progress")
    for item in items[:3]:
        add(f"/api/me/progress/{item}")
    add("/api/me/progress/does-not-exist", ["root"])
    add("/api/me/bookmarks")
    for item in items[:2]:
        add(f"/api/me/bookmarks/{item}")
    add("/api/libraries", ["nobody"])

    users["nobody"] = ""
    identical, expected_diffs, unexpected = 0, 0, []
    for path, who, expected in cases:
        for name in who:
            cn, bn = call(NODE, path, users[name])
            cd, bd = call(DOTNET, path, users[name])
            if (cn, bn) == (cd, bd):
                identical += 1
            elif expected and cd == 501:
                expected_diffs += 1
            else:
                unexpected.append((name, path, cn, cd, bn, bd))

    print(f"[.NET vs Node]    cases: {identical + expected_diffs + len(unexpected)}  identical: {identical}  "
          f"expected differences (501): {expected_diffs}  UNEXPECTED: {len(unexpected)}")
    for name, path, cn, cd, bn, bd in unexpected:
        print(f"\nDIFF {name} {path}  node={cn} dotnet={cd}")
        for line in diff(bn, bd):
            print("   " + line)

    # Phase 2: the front door. Every case, including the ones .NET does not support, must look exactly like Node
    # when it goes through nginx, because nginx sends unsupported requests back to Node.
    front_same, front_bad = 0, []
    for path, who, _ in cases:
        for name in who:
            cn, bn = call(NODE, path, users[name])
            cf, bf = call(NGINX, path, users[name])
            if (cn, bn) == (cf, bf):
                front_same += 1
            else:
                front_bad.append((name, path, cn, cf, bn, bf))

    # Writes always go to Node: do them through nginx, then read through both paths.
    call(NGINX, f"/api/me/progress/{items[2]}", users["reader"], "PATCH",
         {"duration": 100, "progress": 0.5, "currentTime": 50, "isFinished": False})
    call(NGINX, f"/api/me/item/{items[1]}/bookmark", users["reader"], "POST", {"time": 77, "title": "via nginx"})
    for path in (f"/api/me/progress/{items[2]}", "/api/me/progress", "/api/me/bookmarks", "/api/me"):
        cn, bn = call(NODE, path, users["reader"])
        cf, bf = call(NGINX, path, users["reader"])
        if (cn, bn) == (cf, bf):
            front_same += 1
        else:
            front_bad.append(("reader", path + " (after write)", cn, cf, bn, bf))
    first = call(NGINX, f"/api/me/progress/{items[0]}/remove-from-continue-listening", users["reader"])
    second = call(NODE, f"/api/me/progress/{items[0]}/remove-from-continue-listening", users["reader"])
    if first == second:
        front_same += 1
    else:
        front_bad.append(("reader", "remove-from-continue-listening", first[0], second[0], first[1], second[1]))

    print(f"[nginx vs Node]   cases: {front_same + len(front_bad)}  identical: {front_same}  UNEXPECTED: {len(front_bad)}")
    for name, path, cn, cf, bn, bf in front_bad:
        print(f"\nDIFF {name} {path}  node={cn} nginx={cf}")
        for line in diff(bn, bf):
            print("   " + line)

    # Routing: who answered which request (read from the upstream address in nginx's access log).
    probes = [
        ("GET", "/api/libraries", "dotnet"), ("GET", "/api/libraries?include=stats", "node"),
        ("GET", f"/api/libraries/{library}", "dotnet"), ("GET", f"/api/libraries/{library}?include=filterdata", "node"),
        ("PATCH", "/api/libraries/does-not-exist", "node"),
        ("GET", f"/api/libraries/{library}/items", "dotnet"),
        ("GET", f"/api/libraries/{library}/items?limit=2&page=1&sort=media.metadata.title&desc=1&minified=1", "dotnet"),
        ("GET", f"/api/libraries/{library}/items?filter=authors.abc", "node"),
        ("GET", f"/api/libraries/{library}/items?include=rssfeed", "node"),
        ("GET", f"/api/libraries/{library}/items?sort=progress", "node"),
        ("GET", f"/api/items/{items[0]}", "dotnet"), ("GET", f"/api/items/{items[0]}?expanded=1", "node"),
        ("GET", "/api/me", "dotnet"), ("GET", "/api/me?token=x", "node"),
        ("GET", "/api/users", "dotnet"), ("GET", "/api/users?include=latestSession", "node"),
        ("GET", f"/api/users/{root_user_id}", "node"),
        ("GET", "/api/me/progress", "dotnet"), ("GET", f"/api/me/progress/{items[0]}", "dotnet"),
        ("GET", f"/api/me/progress/{items[0]}/remove-from-continue-listening", "node"),
        ("PATCH", f"/api/me/progress/{items[0]}", "node"), ("PATCH", "/api/me/progress/batch/update", "node"),
        ("GET", "/api/me/bookmarks", "dotnet"), ("GET", f"/api/me/bookmarks/{items[0]}", "dotnet"),
        ("POST", f"/api/me/item/{items[0]}/bookmark", "node"),
        ("GET", "/status", "node"),
    ]
    for method, path, _ in probes:
        call(NGINX, path, users["reader"], method, {} if method in ("PATCH", "POST") else None)
    log = compose("logs", "--no-color", "nginx").stdout
    routing_bad = []
    for method, path, want in probes:
        needle = f'"{method} {path} HTTP/1.1"'
        lines = [line for line in log.splitlines() if needle in line and "upstream=" in line]
        got = None
        if lines:
            got = "dotnet" if lines[-1].rstrip().endswith(":8080") else "node"
        if got != want:
            routing_bad.append((method, path, want, got))
    print(f"[routing]         probes: {len(probes)}  wrong: {len(routing_bad)}")
    for method, path, want, got in routing_bad:
        print(f"WRONG {method} {path}: wanted {want}, got {got}")

    sys.exit(1 if (unexpected or front_bad or routing_bad) else 0)


def diff(a, b, prefix=""):
    lines = []
    if isinstance(a, dict) and isinstance(b, dict):
        for key in sorted(set(a) | set(b)):
            if key not in a:
                lines.append(f"only .NET: {prefix}{key} = {json.dumps(b[key])[:70]}")
            elif key not in b:
                lines.append(f"only Node: {prefix}{key} = {json.dumps(a[key])[:70]}")
            else:
                lines += diff(a[key], b[key], f"{prefix}{key}.")
    elif isinstance(a, list) and isinstance(b, list) and len(a) == len(b):
        for index, (x, y) in enumerate(zip(a, b)):
            lines += diff(x, y, f"{prefix}{index}.")
    elif a != b:
        lines.append(f"differs: {prefix} node={json.dumps(a)[:70]} dotnet={json.dumps(b)[:70]}")
    return lines[:40]


if __name__ == "__main__":
    main()
