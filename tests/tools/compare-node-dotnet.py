# Sends the same requests to Node (localhost:13378) and .NET (localhost:18081) and diffs status and body.
# Needs token.txt (root access token) and libid.txt (book library id) in the working directory. Setup is in docs/research/library-items-slice.md.
import json,urllib.request,urllib.error,urllib.parse
N='http://localhost:13378';D='http://localhost:18081'
root=open('token.txt').read().strip();L=open('libid.txt').read().strip()
def call(base,path,tok,method='GET',body=None):
    req=urllib.request.Request(base+path,method=method,data=json.dumps(body).encode() if body else None,headers={'Authorization':'Bearer '+tok,'Content-Type':'application/json','x-return-tokens':'true'})
    try:
        r=urllib.request.urlopen(req);raw=r.read().decode();code=r.status
    except urllib.error.HTTPError as e: raw=e.read().decode();code=e.code
    try: return code,json.loads(raw)
    except: return code,raw
def login(u,p):
    c,d=call(N,'/login','x',method='POST',body={'username':u,'password':p}); return d['user']['accessToken']
# extra users
c,d=call(N,'/api/users',root,'POST',{'username':'reader','password':'readerpw','type':'user','isActive':True,'permissions':{'download':True,'update':False,'delete':False,'upload':False,'accessAllLibraries':True,'accessAllTags':True,'accessExplicitContent':False}}); print('create reader',c)
c,d=call(N,'/api/users',root,'POST',{'username':'nolib','password':'nolibpw','type':'user','isActive':True,'permissions':{'accessAllLibraries':False,'accessAllTags':True,'accessExplicitContent':True},'librariesAccessible':['00000000-0000-4000-8000-000000000000']}); print('create nolib',c)
toks={'root':root,'reader':login('reader','readerpw'),'nolib':login('nolib','nolibpw')}
c,lst=call(N,f'/api/libraries/{L}/items?sort=addedAt',root); ids=[r['id'] for r in lst['results']]
paths=[]
for i in ids: paths.append((f'/api/items/{i}',['root','reader','nolib']))
paths.append(('/api/items/does-not-exist',['root']))
for s in ['addedAt','size','birthtimeMs','mtimeMs','media.duration','media.metadata.publishedYear','media.metadata.authorName','media.metadata.authorNameLF','media.metadata.title','sequence']:
    for dsc in ['0','1']:
        paths.append((f'/api/libraries/{L}/items?sort={urllib.parse.quote(s)}&desc={dsc}',['root','reader']))
for q in ['limit=2&page=0&sort=size','limit=2&page=1&sort=size','limit=2&page=5&sort=size','limit=0&sort=size','limit=abc&sort=size','sort=size&minified=1','limit=-1','page=1.5','limit=1&page=-2']:
    paths.append((f'/api/libraries/{L}/items?{q}',['root']))
paths+= [(f'/api/libraries/{L}/items?sort=size',['nolib']),('/api/libraries/nope/items',['root'])]
same=diff=0;rows=[]
def norm_tie(r): return r
for p,users in paths:
    for u in users:
        cn,bn=call(N,p,toks[u]);cd,bd=call(D,p,toks[u])
        ok=(cn==cd and bn==bd)
        if ok: same+=1
        else:
            diff+=1; rows.append((p,u,cn,cd))
            if isinstance(bn,dict) and isinstance(bd,dict):
                def walk(a,b,pre=''):
                    if isinstance(a,dict) and isinstance(b,dict):
                        for k in sorted(set(a)|set(b)):
                            if k not in a: print('   only .NET',pre+k)
                            elif k not in b: print('   only Node',pre+k)
                            else: walk(a[k],b[k],pre+k+'.')
                    elif isinstance(a,list) and isinstance(b,list) and len(a)==len(b):
                        for i,(x,y) in enumerate(zip(a,b)): walk(x,y,pre+f'{i}.')
                    elif a!=b: print('   differs',pre,repr(a)[:70],'|',repr(b)[:70])
                print('DIFF',u,p,cn,cd); walk(bn,bd)
            else: print('DIFF',u,p,cn,cd,str(bn)[:60],'|',str(bd)[:60])
print('identical',same,'different',diff)
