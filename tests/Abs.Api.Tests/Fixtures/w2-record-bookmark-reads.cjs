const fs=require('fs'),path=require('path');
const source=process.argv[2];
const User=require(path.join(source,'server/models/User'));
const Me=require(path.join(source,'server/controllers/MeController'));
const Database=require(path.join(source,'server/Database'));
const data=JSON.parse(fs.readFileSync(path.join(__dirname,'w2-bookmark-input.json')));
Object.defineProperty(Database,'libraryItemModel',{value:{getExpandedById:async id=>data.items.find(x=>x.id===id)||null}});
(async()=>{
 const output=[];
 for(const test of data.cases){
  const user=Object.assign(Object.create(User.prototype),data.users.find(x=>x.id===test.user));
  let result;
  const res={json:body=>{result={status:200,body}},sendStatus:status=>{result={status,body:status===403?'Forbidden':'Not Found'}}};
  await Me[test.item?'getBookmarksForLibraryItem':'getAllBookmarks']({user,params:{libraryItemId:test.item}},res);
  output.push({name:test.name,...result});
 }
 fs.writeFileSync(path.join(__dirname,'w2-node-bookmark-reads.json'),JSON.stringify(output,null,2)+'\n');
})().catch(e=>{console.error(e);process.exit(1)});
