// Method-level Node controller/model + real SQLite persistence comparison.
// Media lookup and socket boundary are simulated; not a complete Node HTTP integration test.
const fs=require('fs'),path=require('path');
const {Sequelize,DataTypes}=require('sequelize');
const source=process.argv[2], read=n=>JSON.parse(fs.readFileSync(path.join(__dirname,n)));
const Progress=require(path.join(source,'server/models/MediaProgress'));
const User=require(path.join(source,'server/models/User'));
const Me=require(path.join(source,'server/controllers/MeController'));
const Database=require(path.join(source,'server/Database'));
const Socket=require(path.join(source,'server/SocketAuthority'));
(async()=>{
 const db=new Sequelize({dialect:'sqlite',storage:':memory:',logging:false});
 for(const name of ['book','podcastEpisode','user','podcast','libraryItem'])
  db.define(name,{id:{type:DataTypes.STRING,primaryKey:true}},{timestamps:false});
 db.models.user.mediaProgressRemoved=User.mediaProgressRemoved.bind(User);
 Progress.init(db);await db.models.user.sync();
 await db.query(fs.readFileSync(path.join(__dirname,'w2-progress-schema.sql'),'utf8'));
 for(const id of ['w2-alice','w2-bob'])await db.models.user.create({id});
 Database.sequelize=db;
 const initial=read('w2-progress-input.json'), outputs=[];
 const books={'w2-item':'w2-book','w2-new-item':'w2-new-book','w2-private-item':'w2-bob-book'};
 for(const test of read('w2-batch-delete-cases.json')){
  await Progress.destroy({where:{},hooks:false});
  for(const x of initial)await Progress.create({...x,createdAt:new Date(x.createdAt),updatedAt:new Date(x.updatedAt),
   finishedAt:x.finishedAt?new Date(x.finishedAt):null},{silent:true});
  const user={id:'w2-alice',username:'synthetic',sequelize:db,
   mediaProgresses:await Progress.findAll({where:{userId:'w2-alice'}}),
   createUpdateMediaProgressFromPayload:User.prototype.createUpdateMediaProgressFromPayload,
   toOldJSONForBrowser:()=>({})};
  db.models.libraryItem.findByPk=async id=>{
   if(id==='w2-podcast')return {id,mediaType:'podcast'};
   if(!books[id])return null;
   return {id,mediaType:'book',media:{id:books[id],
    mediaProgresses:await Progress.findAll({where:{userId:user.id,mediaItemId:books[id]}})}};
  };
  db.models.podcastEpisode.findByPk=async id=>id==='w2-episode'?{id,podcastId:'w2-podcast-media',
   mediaProgresses:await Progress.findAll({where:{userId:user.id,mediaItemId:id}})}:null;
  let events=0;Socket.clientEmitter=()=>{events++};
  const statuses=[],messages=[],before=Date.now();
  for(let i=0;i<(test.repeat||1);i++){
   const res={status(code){this.code=code;return this},
    send(message){statuses.push(this.code);messages.push(message)},
    sendStatus(code){statuses.push(code);messages.push(code===200?'OK':'Not Found')}};
   const req={user,body:test.payload,params:{id:test.id}};
   await Me[test.method==='DELETE'?'removeMediaProgress':'batchUpdateMediaProgress'](req,res);
  }
  const after=Date.now(),rows=(await Progress.findAll()).map(p=>p.getOldMediaProgress());
  for(const row of rows){
   if(!initial.some(x=>x.id===row.id))row.id='$generated-id';
   for(const field of ['lastUpdate','startedAt','finishedAt'])
    if(row[field]!==null&&row[field]>=before&&row[field]<=after)row[field]='$generated-time';
  }
  rows.sort((a,b)=>(a.userId+'/'+a.mediaItemId).localeCompare(b.userId+'/'+b.mediaItemId));
  outputs.push({name:test.name,statuses,messages,rows,events});
 }
 fs.writeFileSync(path.join(__dirname,'w2-node-batch-delete.json'),JSON.stringify(outputs,null,2)+'\n');
 await db.close();
})().catch(e=>{console.error(e);process.exit(1)});
