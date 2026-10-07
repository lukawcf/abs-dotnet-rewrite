# Impact of a missing `user_updated` socket event (R3)

Source: G:\Project\abs-upstream (shallow clone, read-only). Paths below are relative to it.

## 1. Who handles `user_updated`, and what state changes

| Handler | Registration | Effect |
|---|---|---|
| `client/layouts/default.vue:270-274` (`userUpdated`) | `default.vue:439` | If `user.id` equals the current user, runs `$store.commit('user/setUser', user)`. This replaces the whole `state.user` object (`client/store/user.js:173-175`). That object holds `mediaProgress`, `bookmarks` and `seriesHideFromContinueListening`. |
| `client/components/app/BookShelfCategorized.vue:302-313` (`userUpdated`) | `:473` (on), `:490` (off) | Removes series and items that are hidden from the "continue listening/series/reading" shelves, using `seriesHideFromContinueListening` and `mediaProgress[].hideFromContinueListening`. |
| `client/components/tables/UsersTable.vue:141` (`addUpdateUser`) | `:164` (on), `:175` (off) | Admin users table only. |

Server emit sites: `server/controllers/MeController.js`
- 264: `DELETE /me/progress/:id`
- 286: `PATCH /me/progress/:libraryItemId/:episodeId?` (route at `server/routers/ApiRouter.js:185`)
- 315: `PATCH /me/progress/batch/update`
- 350, 388, 422: create, update and remove bookmark (routes at `ApiRouter.js:187-189`)
- 540, 559: series remove/re-add to continue listening
- 584: remove item from continue listening

`UserController.js:347` and `:424` also emit it, for admin edits.

`SocketAuthority.clientEmitter` (`server/SocketAuthority.js:68-78`) sends the event to every connected socket of that user. That covers the same tab, other tabs and other devices.

## 2. Does the same tab depend on the event?

Yes. The PATCH response carries no data, and the client does not update its own state from it.

- Server: `MeController.js:286-287` emits `user_updated` and then returns `res.sendStatus(200)`, an empty body.
- Client callers only clear their spinner in `.then()`. They never commit to the store:
  - `client/pages/item/_id/index.vue:522-526` (mark finished/unfinished)
  - `client/components/cards/LazyBookCard.vue:749-753`
  - Others pass no store update either: `LazyEpisodeRow.vue:209`, `BookTableRow.vue:188`, `podcast/latest.vue:177`, `Appbar.vue:320`, `BookShelfToolbar.vue:551`, `LazyEpisodesTable.vue:353`.
- Progress UI reads `state.user.mediaProgress` through the getter `user/getUserMediaProgress` (`store/user.js:31-39`). The only writers are `setUser` (via `user_updated`) and `updateMediaProgress` (`store/user.js:185-197`).
- `updateMediaProgress` is called only from the `user_item_progress_updated` handler (`default.vue:293-294`, registered at `:444`). The server emits that event only from `server/managers/PlaybackSessionManager.js:265` and `:402`, i.e. from playback session sync and close. It is not emitted by `PATCH /me/progress`.
- Bookmarks: `client/components/modals/BookmarksModal.vue:96-103` and `:117-125` only show a toast after the HTTP call. The list is read from `state.user.bookmarks` (`store/user.js:40-43`), which is refreshed only by `user_updated`. Bookmark routes stay in Node unless moved, so they are not affected by this change, but they use the same mechanism.
- Continue-listening shelf: items hidden from it are removed only in `BookShelfCategorized.userUpdated` (`:302-313`). The shelf contents come from a fetch (`fetchCategories`), not from the PATCH response.

Conclusion: progress bars, finished badges, the "mark as finished" toggle state, bookmark lists and the continue-listening shelf in the same tab all rely on `user_updated`. Live playback progress is the exception: it flows through `user_item_progress_updated` from session sync, which is unaffected.

## 3. If .NET handles `PATCH /me/progress` and Node never emits `user_updated`

The database row would be correct, because both processes use the same SQLite file. Only the client's cached copy of the user goes stale. These points are inferred from the code above, not tested.

- **Same tab:** the request succeeds (200) and the spinner stops, but nothing on screen changes. The finished/unfinished toggle and progress bar keep the old value. They update only after a full page reload, re-login or other re-fetch of `/api/me`. The user will probably click again, and `isFinished: !this.userIsFinished` (`index.vue:519`) would send the same payload a second time.
- **Other tabs of the same browser:** identical staleness. No event reaches them either, and they have no other refresh path.
- **Other devices (web or mobile apps):** the same. Stale until restart or reload. Because the PATCH route never emits `user_item_progress_updated`, apps relying on socket events would also miss the change. This applies only to apps that listen to the socket. Apps that poll or read `/api/me` on open will see the correct data.
- Playback sync (`/api/session/*`) stays in Node, so progress made by actually listening still pushes `user_item_progress_updated` to all clients.

## 4. Severity: MEDIUM

- Data is not lost or corrupted, and a reload fixes the display. This is why it is not high.
- It is visible in common actions: mark finished, mark unfinished, batch mark and the progress bars after them. The UI looks as if the action failed, which invites repeat clicks and bug reports. This is why it is not low.
- Cross-device and cross-tab sync, a core feature of the product, would silently stop for these actions.
- Mitigations:
  1. Have .NET call a Node internal endpoint that emits the event.
  2. Have .NET emit the socket event itself with the same payload (`user.toOldJSONForBrowser()` shape).
  3. Keep the route in Node.
  Whichever is chosen, the payload must be the full user JSON, because `setUser` replaces the whole object (`store/user.js:173-175`).
