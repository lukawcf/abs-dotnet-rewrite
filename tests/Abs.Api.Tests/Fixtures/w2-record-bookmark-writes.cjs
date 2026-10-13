// Real Node controller, User methods and SQLite persistence; expanded-item/socket boundaries simulated.
const fs=require('fs'),path=require('path'),{Sequelize}=require('sequelize');
const source=process.argv[2],read=n=>JSON.parse(fs.readFileSync(path.join(__dirname,n)));
const User=require(path.join(source,'server/models/User'));
const Me=require(path.join(source,'server/controllers/MeController'));
const Database=require(path.join(source,'server/Database'));
const Socket=require(path.join(source,'server/SocketAuthority'));
const input=read('w2-bookmark-input.json');
Object.defineProperty(Database,'libraryItemModel',{value:{getExpandedById:async id=>input.items.find(x=>x.id===id)||null}});
Socket.clientEmitter=()=>{};User.prototype.toOldJSONForBrowser=()=>({});
(async()=>{
 const db=new Sequelize({dialect:'sqlite',storage:':memory:',logging:false});
 User.init(db);
 const ddl=fs.readFileSync(path.join(__dirname,'schema.sql'),'utf8').match(/CREATE TABLE `users`[^;]+;/)[0];
 await db.query(ddl);
 const output=[];
 for(const test of read('w2-bookmark-write-cases.json')){
  await User.destroy({where:{},hooks:false});
  for(const u of input.users)await User.create({...JSON.parse(JSON.stringify(u)),
   createdAt:new Date('2026-10-01T00:00:00.000Z'),updatedAt:new Date('2026-10-01T00:00:00.000Z')},{silent:true});
  const user=await User.findByPk(test.user||'w2-bm-alice');
  let result;
  const res={code:200,status(n){this.code=n;return this},
   send(body){result={status:this.code,body}},
   sendStatus(status){result={status,body:status===200?'OK':status===403?'Forbidden':'Not Found'}},
   json(body){result={status:200,body:JSON.parse(JSON.stringify(body))}}};
  const before=Date.now();
  await Me[test.method==='POST'?'createBookmark':test.method==='PATCH'?'updateBookmark':'removeBookmark'](
   {user,params:{id:test.item||'w2-item',time:test.time},body:test.payload},res);
  const rows=(await User.findAll({order:[['id','ASC']]})).map(u=>({id:u.id,bookmarks:u.bookmarks,updatedAt:u.updatedAt.valueOf()}));
  const after=Date.now();
  function normalize(value){
   if(!value||typeof value!=='object')return;
   for(const key of Object.keys(value)){
    if(['createdAt','updatedAt'].includes(key)&&typeof value[key]==='number'&&value[key]>=before&&value[key]<=after)
     value[key]='$generated-time';
    else normalize(value[key]);
   }
  }
  normalize(result);normalize(rows);
  output.push({name:test.name,...result,users:rows});
 }
 fs.writeFileSync(path.join(__dirname,'w2-node-bookmark-writes.json'),JSON.stringify(output,null,2)+'\n');
 await db.close();
})().catch(e=>{console.error(e);process.exit(1)});
