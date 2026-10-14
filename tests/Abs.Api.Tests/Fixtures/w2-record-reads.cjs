// Runs real v2.37.1 methods, not a reimplementation. No HTTP/auth transport.
const fs = require('fs');
const path = require('path');
const source = process.argv[2];
const MediaProgress = require(path.join(source, 'server/models/MediaProgress'));
const User = require(path.join(source, 'server/models/User'));
const Me = require(path.join(source, 'server/controllers/MeController'));
const inputs = JSON.parse(fs.readFileSync(path.join(__dirname, 'w2-progress-input.json')));
const rows = inputs.map(x => ({...x,
  createdAt: new Date(x.createdAt), updatedAt: new Date(x.updatedAt),
  finishedAt: x.finishedAt ? new Date(x.finishedAt) : null,
  getOldMediaProgress: MediaProgress.prototype.getOldMediaProgress
}));
const user = {mediaProgresses: rows.filter(x => x.userId === 'w2-alice'),
  getOldMediaProgress: User.prototype.getOldMediaProgress};
async function capture(params, all = false) {
  let result;
  const res = {json: body => {result = {status:200,body}},
    sendStatus: status => {result = {status,body:'Not Found'}}};
  await Me[all ? 'getAllMediaProgress' : 'getMediaProgress']({user,params},res);
  return result;
}
(async () => {
  const expected = {
    all: await capture({},true),
    book: await capture({id:'w2-item'}),
    episode: await capture({id:'w2-podcast',episodeId:'w2-episode'}),
    missing: await capture({id:'w2-book'}),
    otherUser: await capture({id:'w2-private-item'})
  };
  fs.writeFileSync(path.join(__dirname,'w2-node-progress-reads.json'),JSON.stringify(expected,null,2)+'\n');
})().catch(e => {console.error(e);process.exit(1)});
