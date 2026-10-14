// Real User creation/update logic and real MediaProgress persistence.
// Item/episode lookup is simulated; HTTP, auth, socket emission and Node caches are not verified.
const fs = require('fs'), path = require('path');
const { Sequelize, DataTypes } = require('sequelize');
const source = process.argv[2];
const MediaProgress = require(path.join(source,'server/models/MediaProgress'));
const User = require(path.join(source,'server/models/User'));
const read = name => JSON.parse(fs.readFileSync(path.join(__dirname,name)));
(async () => {
 const db = new Sequelize({dialect:'sqlite',storage:':memory:',logging:false});
 const attrs = {id:{type:DataTypes.STRING,primaryKey:true}};
 for (const name of ['book','podcastEpisode','user','podcast','libraryItem']) db.define(name,attrs,{timestamps:false});
 MediaProgress.init(db);
 await db.models.user.sync(); // Minimal synthetic FK parent; progress uses extracted real DDL.
 await db.query(fs.readFileSync(path.join(__dirname,'w2-progress-schema.sql'),'utf8'));
 for(const id of ['w2-alice','w2-bob','w2-empty']) await db.models.user.create({id});
 const initial = read('w2-progress-input.json');
 const outputs = [];
 const books = {'w2-item':'w2-book','w2-new-item':'w2-new-book','w2-private-item':'w2-bob-book'};
 for(const test of read('w2-write-cases.json')) {
  await MediaProgress.destroy({where:{},hooks:false});
  for(const x of initial) await MediaProgress.create({...x,createdAt:new Date(x.createdAt),updatedAt:new Date(x.updatedAt),
    finishedAt:x.finishedAt?new Date(x.finishedAt):null},{silent:true});
  const userId = test.user || 'w2-alice';
  const user = {id:userId,mediaProgresses:await MediaProgress.findAll({where:{userId}}),
    sequelize:db};
  db.models.libraryItem.findByPk = async id => {
    if(id === 'w2-podcast') return {id,mediaType:'podcast'};
    if(!books[id]) return null;
    return {id,mediaType:'book',media:{id:books[id],
      mediaProgresses:await MediaProgress.findAll({where:{userId,mediaItemId:books[id]}})}};
  };
  db.models.podcastEpisode.findByPk = async id => {
    if(!['w2-episode','w2-new-episode'].includes(id)) return null;
    return {id,podcastId:'w2-podcast-media',
      mediaProgresses:await MediaProgress.findAll({where:{userId,mediaItemId:id}})};
  };
  const payload = {...test.patch,libraryItemId:test.item,episodeId:test.episode};
  const before = Date.now();
  const result = await User.prototype.createUpdateMediaProgressFromPayload.call(user,payload);
  const after = Date.now();
  const output = {name:test.name,status:result.statusCode || 200,message:result.error || 'OK'};
  if(result.mediaProgress) {
    await result.mediaProgress.reload();
    const row = result.mediaProgress.getOldMediaProgress();
    if(!initial.some(x=>x.id===row.id)) row.id = '$generated-id';
    for(const field of ['lastUpdate','startedAt','finishedAt'])
      if(row[field] !== null && row[field] >= before && row[field] <= after) row[field] = '$generated-time';
    output.row = row;
  }
  outputs.push(output);
 }
 fs.writeFileSync(path.join(__dirname,'w2-node-progress-writes.json'),JSON.stringify(outputs,null,2)+'\n');
 await db.close();
})().catch(e => {console.error(e);process.exit(1)});
