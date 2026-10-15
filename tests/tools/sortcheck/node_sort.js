const names = JSON.parse(require('fs').readFileSync('/data/names.json', 'utf8'))
const collator = new Intl.Collator(undefined, { numeric: true, sensitivity: 'base' })
// stable sort like fast-sort: ties keep input order
const out = names.map((n, i) => [n, i]).sort((a, b) => collator.compare(a[0], b[0]) || a[1] - b[1]).map((x) => x[0])
require('fs').writeFileSync('/data/node_sorted.json', JSON.stringify(out))
console.log('node ICU', process.versions.icu, 'unicode', process.versions.unicode, 'locale', new Intl.Collator().resolvedOptions().locale)
