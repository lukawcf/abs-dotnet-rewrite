using System.Globalization;
using System.Text.Json;

var names = JsonSerializer.Deserialize<string[]>(File.ReadAllText("/data/names.json"))!;
var node = JsonSerializer.Deserialize<string[]>(File.ReadAllText("/data/node_sorted.json"))!;
var info = CultureInfo.GetCultureInfo("en-US").CompareInfo;
const CompareOptions Base = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace | CompareOptions.IgnoreWidth | CompareOptions.IgnoreKanaType;
Console.WriteLine($".NET {Environment.Version}, collation version {info.Version}, invariant mode: {AppContext.TryGetSwitch("System.Globalization.Invariant", out var inv) && inv}");

int Plain(string a, string b) => info.Compare(a, b, Base);

// Natural: runs of decimal digits compare by value, everything else by collation.
List<(bool Digits, string Text)> Chunks(string s)
{
    var list = new List<(bool, string)>();
    int i = 0;
    while (i < s.Length)
    {
        bool d = char.IsDigit(s, i);
        int j = i;
        while (j < s.Length && char.IsDigit(s, j) == d) j++;
        list.Add((d, s[i..j]));
        i = j;
    }
    return list;
}

string Normalise(string digits)
{
    var value = new string(digits.Select(c => (char)('0' + (int)char.GetNumericValue(c))).ToArray()).TrimStart('0');
    return value.Length == 0 ? "0" : value;
}

int Natural(string a, string b)
{
    var x = Chunks(a); var y = Chunks(b);
    for (int k = 0; k < Math.Min(x.Count, y.Count); k++)
    {
        int c;
        if (x[k].Item1 && y[k].Item1)
        {
            var p = Normalise(x[k].Item2); var q = Normalise(y[k].Item2);
            c = p.Length != q.Length ? p.Length.CompareTo(q.Length) : string.CompareOrdinal(p, q);
        }
        else c = info.Compare(x[k].Item2, y[k].Item2, Base);
        if (c != 0) return c;
    }
    return x.Count.CompareTo(y.Count);
}

string[] Sort(Comparison<string> cmp) =>
    names.Select((n, i) => (n, i)).OrderBy(t => t, Comparer<(string n, int i)>.Create((l, r) => { var c = cmp(l.n, r.n); return c != 0 ? c : l.i.CompareTo(r.i); })).Select(t => t.n).ToArray();

foreach (var (label, cmp) in new (string, Comparison<string>)[] { ("plain collation (no numeric)", Plain), ("natural (digit chunks + collation)", Natural) })
{
    var mine = Sort(cmp);
    int diffPositions = Enumerable.Range(0, node.Length).Count(i => node[i] != mine[i]);
    Console.WriteLine($"\n{label}: {diffPositions} of {node.Length} positions differ from Node");
    int shown = 0;
    for (int i = 0; i < node.Length && shown < 12; i++)
        if (node[i] != mine[i]) { Console.WriteLine($"  pos {i,3}: node='{node[i]}'  net='{mine[i]}'"); shown++; }
}
