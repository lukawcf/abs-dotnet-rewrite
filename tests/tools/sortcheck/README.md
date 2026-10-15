# sortcheck

Compares Node's `Intl.Collator(undefined, {numeric: true, sensitivity: 'base'})` sort with a .NET comparer
(`docs/research/filterdata-assessment.md`, section 3).

```bash
# 1. Node's order (inside the audiobookshelf image, ICU of Node)
docker run --rm --entrypoint node -v <this folder>:/data ghcr.io/advplyr/audiobookshelf:latest /data/node_sort.js
# 2. .NET's order (inside the aspnet image) and the comparison
cd net && dotnet publish -c Release -o ../out && cd ..
docker run --rm -v <this folder>:/data --entrypoint dotnet mcr.microsoft.com/dotnet/aspnet:8.0 /data/out/SortCheck.dll
```

Last result (2026-10-15): plain collation 34 of 119 positions differ, digit-chunk comparer 0 of 119.
Not part of AbsRewrite.sln.
