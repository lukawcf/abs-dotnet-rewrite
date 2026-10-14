// Records what real audiobookshelf code returns for the Library Items slice.
// Usage: ABS_UPSTREAM=/path/to/audiobookshelf node tests/tools/record-node-items.js <out-dir>
// Uses an in-memory SQLite database only. Writes items-schema.sql, items-seed.sql and node-items.json to <out-dir>.
const fs = require('fs')
const path = require('path')
const { Sequelize } = require('sequelize')

const upstream = process.env.ABS_UPSTREAM
const outDir = process.argv[2]
if (!upstream || !outDir) {
  console.error('ABS_UPSTREAM and <out-dir> are required')
  process.exit(1)
}

const Database = require(path.join(upstream, 'server/Database'))
const { getTitleIgnorePrefix } = require(path.join(upstream, 'server/utils'))
const parseNameString = require(path.join(upstream, 'server/utils/parsers/parseNameString'))

const LIBRARY_A = 'aeb8a59a-eb09-4444-bd9d-2ca5cf8fa161'
const FOLDER_A = '72033519-7e80-4bb8-bc72-7895b05568bc'
const LIBRARY_B = 'bbbbbbbb-0000-4000-8000-000000000002'
const NEW_TABLES = ['settings', 'authors', 'series', 'books', 'libraryItems', 'bookAuthors', 'bookSeries']

const ts = (minutes) => new Date(Date.UTC(2026, 9, 6, 22, minutes, 0, 123))
const audio = (name, size, extra = {}) => ({ index: 1, ino: `ino-${name}`, metadata: { filename: name, ext: '.mp3', path: `/audiobooks/${name}`, relPath: name, size }, ...extra })

const defaultPrefixes = ['the', 'a', 'an']
const setServerSettings = (sortingIgnorePrefix) => {
  global.ServerSettings = { sortingIgnorePrefix, sortingPrefixes: defaultPrefixes }
}

const authors = [
  { id: 'au-tolkien', name: 'J.R.R. Tolkien' },
  { id: 'au-herbert', name: 'Frank Herbert' },
  { id: 'au-gaiman', name: 'Neil Gaiman' },
  { id: 'au-pratchett', name: 'Terry Pratchett' }
]
const seriesRows = [
  { id: 'se-middle', name: 'Middle-earth' },
  { id: 'se-dune', name: 'Dune' }
]

// id, title, authors (ordered), series [seriesId, sequence], book columns, item columns, minute offset for createdAt
const books = [
  {
    id: 'bk-hobbit', title: 'The Hobbit', authorIds: ['au-tolkien'], series: [['se-middle', '1']],
    publishedYear: '1937', duration: 3600, explicit: false, tags: ['fantasy'], genres: ['Fantasy', 'Adventure'], narrators: ['Rob Inglis'],
    audioFiles: [audio('hobbit1.mp3', 1000), { ...audio('hobbit2.mp3', 2000), index: 2, exclude: true }],
    chapters: [{ id: 0, start: 0, end: 1800, title: 'Chapter 1' }], description: '<p>There and back again</p>',
    item: { id: 'li-hobbit', min: 1, size: 3000, folder: 'The Hobbit' }
  },
  {
    id: 'bk-dune', title: 'Dune', authorIds: ['au-herbert'], series: [['se-dune', '1'], ['se-middle', '2.5']],
    publishedYear: '1965', duration: 7200, explicit: false, tags: ['scifi', 'classic'], genres: ['Science Fiction'], narrators: ['Scott Brick', 'Orlagh Cassidy'],
    audioFiles: [audio('dune.mp3', 5000)], chapters: [], description: 'Spice',
    item: { id: 'li-dune', min: 2, size: 5000, folder: 'Dune' }
  },
  {
    id: 'bk-omens', title: 'A Good Omen', authorIds: ['au-gaiman', 'au-pratchett'], series: [],
    publishedYear: '1990', duration: 5400, explicit: true, tags: [], genres: ['Fantasy'], narrators: [],
    audioFiles: [audio('omens.mp3', 4000)], chapters: [], description: null, ebookFile: null,
    item: { id: 'li-omens', min: 3, size: 4000, folder: 'Good Omens' }
  },
  {
    id: 'bk-alpha', title: 'Alpha', authorIds: [], series: [],
    publishedYear: null, duration: 0, explicit: false, tags: ['scifi'], genres: [], narrators: [],
    audioFiles: [], chapters: [], description: null,
    ebookFile: { ino: 'ino-alpha', metadata: { filename: 'alpha.epub', ext: '.epub', path: '/audiobooks/alpha.epub', relPath: 'alpha.epub', size: 700 }, ebookFormat: 'epub' },
    item: { id: 'li-alpha', min: 4, size: 700, folder: 'alpha' }
  },
  {
    id: 'bk-cabbage', title: 'cabbage patch', authorIds: ['au-pratchett'], series: [],
    publishedYear: '2001', duration: 900, explicit: false, tags: ['nature'], genres: ['Nature'], narrators: ['Someone'],
    audioFiles: [audio('zebra.mp3', 900)], chapters: [], description: 'Black and white',
    item: { id: 'li-cabbage', min: 5, size: 900, folder: 'cabbage' }
  },
  {
    id: 'bk-empty', title: 'Empty Shell', authorIds: [], series: [],
    publishedYear: '999', duration: 0, explicit: false, tags: [], genres: [], narrators: [],
    audioFiles: [], chapters: [], description: null,
    item: { id: 'li-empty', min: 6, size: 1234, folder: 'empty', nullFlags: true }
  }
]

const authorName = (b) => b.authorIds.map((id) => authors.find((a) => a.id === id).name)

async function buildDb() {
  setServerSettings(false)
  Database.sequelize = new Sequelize({ dialect: 'sqlite', storage: ':memory:', logging: false })
  Database.sequelize.uppercaseFirst = (str) => (str ? `${str[0].toUpperCase()}${str.substr(1)}` : '')
  await Database.buildModels()

  await Database.libraryModel.create({ id: LIBRARY_A, name: 'Test Book', mediaType: 'book' })
  await Database.libraryFolderModel.create({ id: FOLDER_A, path: '/audiobooks', libraryId: LIBRARY_A })
  await Database.libraryModel.create({ id: LIBRARY_B, name: 'Podcasts', mediaType: 'podcast' })

  await Database.sequelize.models.setting.create({ key: 'server-settings', value: { id: 'server-settings', sortingIgnorePrefix: false, sortingPrefixes: defaultPrefixes } })

  for (const a of authors) {
    await Database.authorModel.create({ id: a.id, name: a.name, lastFirst: parseNameString.nameToLastFirst(a.name), libraryId: LIBRARY_A, createdAt: ts(0), updatedAt: ts(0) })
  }
  for (const s of seriesRows) {
    await Database.seriesModel.create({ id: s.id, name: s.name, nameIgnorePrefix: getTitleIgnorePrefix(s.name), libraryId: LIBRARY_A, createdAt: ts(0), updatedAt: ts(0) })
  }

  for (const b of books) {
    const names = authorName(b)
    await Database.bookModel.create({
      id: b.id, title: b.title, titleIgnorePrefix: getTitleIgnorePrefix(b.title), publishedYear: b.publishedYear, duration: b.duration,
      explicit: b.explicit, tags: b.tags, genres: b.genres, narrators: b.narrators, audioFiles: b.audioFiles, chapters: b.chapters,
      description: b.description, ebookFile: b.ebookFile || null, createdAt: ts(b.item.min), updatedAt: ts(b.item.min)
    })
    await Database.libraryItemModel.create({
      id: b.item.id, ino: `ino-${b.item.id}`, path: `/audiobooks/${b.item.folder}`, relPath: b.item.folder, mediaId: b.id, mediaType: 'book',
      isFile: false, isMissing: b.item.nullFlags ? null : false, isInvalid: b.item.nullFlags ? null : false, mtime: ts(b.item.min + 10), ctime: ts(b.item.min + 11), birthtime: ts(b.item.min + 12),
      size: b.item.size, lastScan: ts(30), lastScanVersion: '2.37.1', libraryFiles: [{ ino: `ino-${b.item.id}`, metadata: { filename: 'f', ext: '.mp3', path: '/f', relPath: 'f', size: 1 }, isSupplementary: false, addedAt: 1, updatedAt: 2, fileType: 'audio' }],
      extraData: b.item.id === 'li-dune' ? { oldLibraryItemId: 'li_old' } : {},
      title: b.title, titleIgnorePrefix: getTitleIgnorePrefix(b.title),
      authorNamesFirstLast: names.join(', '), authorNamesLastFirst: names.map((n) => parseNameString.nameToLastFirst(n)).join(', '),
      libraryId: LIBRARY_A, libraryFolderId: FOLDER_A, createdAt: ts(b.item.min), updatedAt: ts(b.item.min + 20)
    })
    let i = 0
    for (const authorId of b.authorIds) {
      await Database.bookAuthorModel.create({ id: `ba-${b.id}-${authorId}`, bookId: b.id, authorId, createdAt: ts(b.item.min + i++) })
    }
    i = 0
    for (const [seriesId, sequence] of b.series) {
      await Database.bookSeriesModel.create({ id: `bs-${b.id}-${seriesId}`, bookId: b.id, seriesId, sequence, createdAt: ts(b.item.min + i++) })
    }
  }

  await Database.libraryItemModel.create({
    id: 'li-podcast', ino: 'ino-pod', path: '/podcasts/p', relPath: 'p', mediaId: 'pc-missing', mediaType: 'podcast', isFile: false,
    mtime: ts(40), ctime: ts(40), birthtime: ts(40), size: 0, libraryFiles: [], extraData: {}, libraryId: LIBRARY_B, createdAt: ts(40), updatedAt: ts(40)
  })
}

const asJson = (value) => JSON.parse(JSON.stringify(value))

async function recordList(libraryId, query, user) {
  const library = await Database.libraryModel.findByIdWithFolders(libraryId)
  const include = (query.include || '').split(',').map((v) => v.trim().toLowerCase()).filter((v) => !!v)
  const payload = {
    results: [], total: undefined, limit: Number(query.limit ?? 0) || 0, page: Number(query.page ?? 0) || 0, sortBy: query.sort, sortDesc: query.desc === '1',
    filterBy: query.filter, mediaType: library.mediaType, minified: query.minified === '1', collapseseries: query.collapseseries === '1', include: include.join(',')
  }
  payload.offset = payload.page * payload.limit
  const { libraryItems, count } = await Database.libraryItemModel.getByFilterAndSort(library, user, payload)
  payload.results = libraryItems
  payload.total = count
  return asJson(payload)
}

async function main() {
  await buildDb()
  const fullUser = { canAccessExplicitContent: true, permissions: { accessAllTags: true }, isAdminOrUp: true }
  const noExplicitUser = { canAccessExplicitContent: false, permissions: { accessAllTags: true }, isAdminOrUp: false }

  const out = { items: {}, lists: {} }
  for (const b of books) {
    const item = await Database.libraryItemModel.getExpandedById(b.item.id)
    out.items[b.item.id] = asJson(item.toOldJSON())
  }

  const scenarios = {
    'title-asc': [{ sort: 'media.metadata.title' }, fullUser],
    'title-desc': [{ sort: 'media.metadata.title', desc: '1' }, fullUser],
    'title-asc-ignore-prefix': [{ sort: 'media.metadata.title' }, fullUser, true],
    'addedAt-desc': [{ sort: 'addedAt', desc: '1' }, fullUser],
    size: [{ sort: 'size' }, fullUser],
    duration: [{ sort: 'media.duration' }, fullUser],
    'published-year-desc': [{ sort: 'media.metadata.publishedYear', desc: '1' }, fullUser],
    'author-name': [{ sort: 'media.metadata.authorName' }, fullUser],
    'author-name-lf': [{ sort: 'media.metadata.authorNameLF' }, fullUser],
    birthtime: [{ sort: 'birthtimeMs' }, fullUser],
    mtime: [{ sort: 'mtimeMs', desc: '1' }, fullUser],
    'page-0-limit-2': [{ sort: 'media.metadata.title', limit: '2', page: '0' }, fullUser],
    'page-1-limit-2': [{ sort: 'media.metadata.title', limit: '2', page: '1' }, fullUser],
    'page-2-limit-2': [{ sort: 'media.metadata.title', limit: '2', page: '2' }, fullUser],
    'page-9-limit-2': [{ sort: 'media.metadata.title', limit: '2', page: '9' }, fullUser],
    'no-explicit': [{ sort: 'media.metadata.title' }, noExplicitUser],
    'minified-flag': [{ sort: 'addedAt', minified: '1' }, fullUser]
  }
  for (const [name, [query, user, ignorePrefix]] of Object.entries(scenarios)) {
    setServerSettings(!!ignorePrefix)
    out.lists[name] = { query, response: await recordList(LIBRARY_A, query, user) }
  }
  setServerSettings(false)
  for (const b of books) {
    // re-record items under default settings (titleIgnorePrefix text uses sortingPrefixes only)
    const item = await Database.libraryItemModel.getExpandedById(b.item.id)
    out.items[b.item.id] = asJson(item.toOldJSON())
  }

  fs.mkdirSync(outDir, { recursive: true })
  fs.writeFileSync(path.join(outDir, 'node-items.json'), JSON.stringify(out, null, 2) + '\n')

  const q = Database.sequelize
  const [tables] = await q.query(`SELECT name, sql FROM sqlite_master WHERE type='table' AND name IN (${NEW_TABLES.map((t) => `'${t}'`).join(',')})`)
  const [indexes] = await q.query(`SELECT name, tbl_name, sql FROM sqlite_master WHERE type='index' AND sql IS NOT NULL AND tbl_name IN (${NEW_TABLES.map((t) => `'${t}'`).join(',')})`)
  const ddl = NEW_TABLES.map((t) => tables.find((x) => x.name === t).sql + ';').concat(indexes.map((i) => i.sql + ';'))
  fs.writeFileSync(path.join(outDir, 'items-schema.sql'), ddl.join('\n') + '\n')

  const inserts = []
  for (const t of NEW_TABLES) {
    const [rows] = await q.query(`SELECT * FROM \`${t}\``)
    for (const row of rows) {
      const cols = Object.keys(row)
      const vals = []
      for (const c of cols) {
        const [[r]] = await q.query(`SELECT quote(:v) AS v`, { replacements: { v: row[c] } })
        vals.push(r.v)
      }
      inserts.push(`INSERT INTO \`${t}\` (${cols.map((c) => `\`${c}\``).join(', ')}) VALUES (${vals.join(', ')});`)
    }
  }
  fs.writeFileSync(path.join(outDir, 'items-seed.sql'), inserts.join('\n') + '\n')
  console.log('wrote', outDir, Object.keys(out.items).length, 'items', Object.keys(out.lists).length, 'lists')
}

main().catch((e) => {
  console.error(e)
  process.exit(1)
})
