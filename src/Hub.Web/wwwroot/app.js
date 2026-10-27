import { ProjectionClient } from './realtime.js';

const $ = (s, root = document) => root.querySelector(s);
const $$ = (s, root = document) => [...root.querySelectorAll(s)];
const esc = s => String(s ?? '').replace(/[&<>"']/g, c => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
const paths = {
 folder:'M3 5h6l2 2h10v13H3z',
 sun:'M12 8a4 4 0 1 1 0 8 4 4 0 0 1 0-8 M12 2v2 M12 20v2 M2 12h2 M20 12h2 M5 5l1.5 1.5 M17.5 17.5 1.5 1.5 M5 19l1.5-1.5 M17.5 6.5 1.5-1.5',
 moon:'M20.5 14A9 9 0 0 1 10 3.5 9 9 0 1 0 20.5 14z',
 pin:'m16 3 5 5-3 1-4 5v3l-3-3-6 7 M5 11l3 3 M5 11h3l5-4 1-3z',
 home:'m3 10 9-7 9 7 M5 9v12h5v-7h4v7h5V9',
 panel:'M3 3h18v18H3z M9 3v18',
 edit:'M12 4H4v16h16v-8 M17 2l5 5-10 10-6 1 1-6z M15 4l5 5',
 grid:'M3 3h7v7H3z M14 3h7v7h-7z M3 14h7v7H3z M14 14h7v7h-7z',
 spark:'m12 3 2.5 6.5L21 12l-6.5 2.5L12 21l-2.5-6.5L3 12l6.5-2.5z M20 2v4 M18 4h4',
 files:'M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z M14 2v6h6 M8 13h8 M8 17h6',
 search:'M21 21l-5-5 M18 10a8 8 0 1 1-16 0 8 8 0 0 1 16 0',
 shield:'M12 2 3 6v6c0 5 9 10 9 10s9-5 9-10V6z m-4 9 3 3 5-5',
 activity:'M2 12h5l3-8 4 16 3-8h5',
 users:'M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2 M16 3a4 4 0 0 1 0 8 M22 21v-2a4 4 0 0 0-3-3.87 M13 7a4 4 0 1 1-8 0 4 4 0 0 1 8 0',
 settings:'M12 8a4 4 0 1 1 0 8 4 4 0 0 1 0-8 M9 2h6l1 4 4 1 2 5-3 3v4l-5 3-3-2-4 1-4-5 2-4-1-4 4-2z',
 logout:'M9 3H4v18h5 M14 8l5 4-5 4 M8 12h12',
 arrow:'M7 17 17 7 M7 7h10v10',
 book:'M12 4v17 M12 4C9 2 5 2 2 3v16c3-1 7-1 10 2 3-3 7-3 10-2V3c-3-1-7-1-10 1',
 layers:'m12 2 10 5-10 5L2 7z M2 12l10 5 10-5 M2 17l10 5 10-5',
 clock:'M12 8v5l3 2 M22 12a10 10 0 1 1-20 0 10 10 0 0 1 20 0',
 mic:'M12 2a3 3 0 0 0-3 3v7a3 3 0 0 0 6 0V5a3 3 0 0 0-3-3 M5 10v2a7 7 0 0 0 14 0v-2 M12 19v3 M8 22h8',
 attach:'m21 11-9 9a6 6 0 0 1-8.5-8.5l10-10a4 4 0 0 1 5.7 5.7L9 16.4a2 2 0 0 1-2.8-2.8L15 5',
 send:'m22 2-7 20-4-9-9-4z M22 2 11 13',
 upload:'M12 16V3 M7 8l5-5 5 5 M3 16v5h18v-5',
 volume:'M11 5 6 9H2v6h4l5 4z M15 8a6 6 0 0 1 0 8 M18 5a10 10 0 0 1 0 14',
 copy:'M8 8h13v13H8z M16 4V2H2v14h2'
};
const icon = name => `<svg class="icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.4" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="${paths[name] || paths.files}"/></svg>`;
$$('[data-icon]').forEach(el => el.innerHTML = icon(el.dataset.icon));
function setTheme(theme){document.documentElement.dataset.theme=theme;$('#theme-label').textContent=theme==='light'?'Night mode':'Light mode';$('#theme-icon').innerHTML=icon(theme==='light'?'moon':'sun');document.querySelector('meta[name="theme-color"]').content=theme==='light'?'#ffffff':'#212121';try{localStorage.setItem('atlas.theme',theme);}catch{}}
let savedTheme='dark';try{if(localStorage.getItem('atlas.theme')==='light')savedTheme='light';}catch{}setTheme(savedTheme);
$('#theme-toggle').onclick=()=>setTheme(document.documentElement.dataset.theme==='light'?'dark':'light');
let csrf, user, view = 'home', documents = [], conversationId = null, runId = null, sourceList = [], imageDocumentId = null;
let eventSource, answerText = '', answerEl, lastMessages = [], recorder, recordingStream, recordingTimeout, activeSpeech;
let sessionEpoch=0;
let lastRunError='', lastQuestion='', docFilter='', docState='all';
let historyChats=[], historyQuery='', menuChat=null;
let docFolderTeam=null, docFolderAccess=null;
const accessName = value => ({Organization:'Organization',Department:'Team',Private:'Private'}[value]||value);
const names = {home:'Overview',chat:'Home',documents:'Knowledge library',search:'Search knowledge',approvals:'Approvals',system:'System & models',team:'Team management',settings:'My account'};
const main = $('#main-content');
const toast = message => { const el=$('#toast'); el.textContent=message; el.classList.remove('hidden'); clearTimeout(el.timer); el.timer=setTimeout(()=>el.classList.add('hidden'),6500); };
async function token(){ csrf=(await (await fetch('/api/auth/csrf')).json()).token; }
async function api(url, options={}) {
 const requestOwner=user?.id;
 const requestEpoch=sessionEpoch;
 const headers = new Headers(options.headers || {});
 if (options.body && !(options.body instanceof FormData)) { headers.set('Content-Type','application/json'); options.body=JSON.stringify(options.body); }
 if (options.method && options.method!=='GET') { if (!csrf) await token(); headers.set('X-CSRF-TOKEN',csrf); }
 const r=await fetch('/api'+url,{...options,headers});
 if(requestOwner&&(requestOwner!==user?.id||requestEpoch!==sessionEpoch))throw Error('Your session changed.');
 if(r.status===401 && url!='/auth/login'){showLogin();throw Error('Please sign in to continue.');}
 if(!r.ok){ let data; try{data=await r.json();}catch{} throw Error(data?.error || (r.status===429?'Too many requests. Please wait a moment.':`Request failed (${r.status}).`)); }
 const body=options.binary?await r.blob():await r.text();
 if(requestOwner&&(requestOwner!==user?.id||requestEpoch!==sessionEpoch))throw Error('Your session changed.');
 return options.binary?body:body?JSON.parse(body):null;
}
const date = s => new Date(s.endsWith('Z')?s:s+'Z').toLocaleDateString('en-US',{month:'short',day:'numeric',year:'numeric'});
const money = n => '$'+Number(n||0).toFixed(4);
const badge = s => `<span class="badge ${['Failed','Rejected','unavailable'].includes(s)?'error':['Queued','Indexing','LexicalOnly','Pending','degraded'].includes(s)?'warn':''}"><span class="status-dot"></span>${esc(s)}</span>`;
const pageActions = button => { $('#view-actions').innerHTML=button; return ''; };
const empty = title => `<div class="empty">${icon('book')}<h3>${esc(title)}</h3></div>`;
const liveViews=new Map();
let activeViewProjection=null;
async function verifyLiveSession(){const response=await fetch('/api/me');return ![401,403].includes(response.status);}
function liveView(name,onData){
 let client=liveViews.get(name);
 if(!client){client=new ProjectionClient(name,{onData,onRevoked:showLogin,verifySession:verifyLiveSession,onError:message=>{
  if(!user)return;toast(message);api('/'+name).then(data=>{if(user)onData(data);}).catch(()=>{});
 }});liveViews.set(name,client);}else client.onData=onData;
 return client;
}
function activateLiveView(name,onData){activeViewProjection?.pause();const client=liveView(name,onData);activeViewProjection=client;client.pause();client.start();}
function showLogin(){
 sessionEpoch++;csrf=null;clearTimeout(recordingTimeout);if(recorder?.state==='recording'){recorder.onstop=null;recorder.stop();}recordingStream?.getTracks().forEach(track=>track.stop());
 if(activeSpeech){activeSpeech.pause();URL.revokeObjectURL(activeSpeech.src);activeSpeech.src='';activeSpeech=null;}
 for(const client of liveViews.values())client.dispose();liveViews.clear();activeViewProjection=null;
 historyChats=[];historyQuery='';documents=[];docFolderTeam=null;docFolderAccess=null;docFilter='';docState='all';lastMessages=[];sourceList=[];answerText='';lastQuestion='';lastRunError='';runId=null;conversationId=null;imageDocumentId=null;menuChat=null;
 $('#chat-history-search').value='';$('#chat-history-search').classList.add('hidden');
 renderHistory();user=null;eventSource?.close();eventSource=null;main.innerHTML='';if($('#modal').open)$('#modal').close();$('#modal-body').innerHTML='';
 $('#account-menu').hidePopover();$('#chat-menu').hidePopover();$('#boot').classList.add('hidden');$('#app-view').classList.add('hidden');$('#login-view').classList.remove('hidden');
}
async function boot(){try{await token();user=await api('/me');await showApp();}catch{showLogin();}}
async function showApp(){
 $('#boot').classList.add('hidden');$('#login-view').classList.add('hidden');$('#app-view').classList.remove('hidden');
 $('#user-name').textContent=user.name;$('#user-role').textContent=user.role==='Admin'?'Workspace administrator':user.department+' · Member';
 $('#user-avatar').textContent=user.name.split(' ').map(s=>s[0]).slice(0,2).join('').toUpperCase();
 $('#rail-avatar').textContent=$('#user-avatar').textContent;
 $$('.admin-only').forEach(x=>x.classList.toggle('hidden',user.role!=='Admin'));
 await token();await recentChats();if(!user)return;const route=readRoute();conversationId=route.conversationId;await navigate(route.view,false);saveRoute(true);
}
$('#login-form').addEventListener('submit',async e=>{
 e.preventDefault();const button=$('button',e.target);button.disabled=true;$('#login-error').textContent='';
 try{const data=new FormData(e.target);await token();user=await api('/auth/login',{method:'POST',body:{email:data.get('email'),password:data.get('password')}});e.target.reset();await showApp();}
 catch(err){$('#login-error').textContent=err.message;}finally{button.disabled=false;}
});
$('#logout').addEventListener('click',async()=>{try{await api('/auth/logout',{method:'POST'});showLogin();await token();}catch(e){toast(e.message);}});
$('#quick-search').onclick=()=>navigate('search');
$('#new-chat').onclick=()=>{conversationId=null;navigate('chat');renderHistory();};
document.addEventListener('keydown',e=>{if((e.ctrlKey||e.metaKey)&&e.key.toLowerCase()==='k'){e.preventDefault();if(user){if(view==='chat'){expandChatSidebar();if(matchMedia('(max-width:760px)').matches)openMobileHistory();else{const field=$('#chat-history-search');field.classList.remove('hidden');field.focus();}}else navigate('search');}}if((e.ctrlKey||e.metaKey)&&e.shiftKey&&e.key.toLowerCase()==='o'){e.preventDefault();if(user)$('#new-chat').click();}});
document.addEventListener('click',e=>{const b=e.target.closest('[data-view]');if(b)navigate(b.dataset.view);});
$('.modal-close').onclick=()=>$('#modal').close();
$('#modal').addEventListener('click',e=>{if(e.target===$('#modal')){const r=e.target.getBoundingClientRect();if(e.clientX<r.left||e.clientX>r.right||e.clientY<r.top||e.clientY>r.bottom)e.target.close();}});
function modal(html){$('#modal-body').innerHTML=html;$('#modal').showModal();}
function readRoute(){const [area,part]=location.hash.slice(1).split('/');if(area==='manage')return {view:names[part]&&part!=='chat'?part:'home',conversationId:null};return {view:area==='overview'?'home':area==='home'?'chat':names[area]?area:'chat',conversationId:area==='chat'&&/^[\da-f-]{36}$/i.test(part||'')?part:null};}
window.addEventListener('popstate',()=>{if(user){const route=readRoute();conversationId=route.conversationId;navigate(route.view,false);}});
function saveRoute(replace=false){const path=view==='chat'?'#chat'+(conversationId?'/'+conversationId:''):view==='home'?'#overview':'#'+view;if(location.hash!==path)history[replace?'replaceState':'pushState'](null,'',path);}
async function navigate(next,writeHistory=true){
 if(user&&user.role!=='Admin'&&['approvals','system','team'].includes(next)){toast('Administrator access required.');next='chat';}
 activeViewProjection?.pause();activeViewProjection=null;
 $('#chat-menu').hidePopover();
 $('#account-menu').hidePopover();$('#view-actions').innerHTML='';
 if(view==='chat' && next!=='chat'){eventSource?.close();eventSource=null;}
 view=next;main.classList.toggle('chat-view',next==='chat');if(writeHistory)saveRoute();$('#breadcrumb').textContent=names[next];$$('[data-view]').forEach(b=>{b.classList.toggle('active',b.dataset.view===next);if(b.dataset.view===next)b.setAttribute('aria-current','page');else b.removeAttribute('aria-current');});$('#account-menu-button').classList.toggle('active',next==='settings');renderHistory();
 main.innerHTML='<div class="skeleton">Opening your workspace…</div>';
 try{await ({home:renderHome,chat:renderChat,documents:renderDocuments,search:renderSearch,approvals:renderApprovals,system:renderSystem,team:renderTeam,settings:renderSettings}[next])();}
 catch(e){main.innerHTML=`<div class="error-box">${esc(e.message)}</div>`;}
}
async function recentChats(){
 if(!user)return;
 const client=liveView('conversations',data=>{if(!user)return;historyChats=data;renderHistory();});client.start();
 if(!client.state){const loaded=await Promise.race([client.ready,new Promise(resolve=>setTimeout(()=>resolve(false),5000))]);if(!loaded&&user){try{historyChats=await api('/conversations');renderHistory();}catch{}}}
}
function renderHistory(){
 const chats=historyChats.filter(c=>c.title.toLowerCase().includes(historyQuery.toLowerCase()));const today=new Date();today.setHours(0,0,0,0);const week=new Date(today);week.setDate(week.getDate()-7);
 const groups=[['Pinned',c=>c.pinned],['Today',c=>new Date(c.createdAt.endsWith('Z')?c.createdAt:c.createdAt+'Z')>=today],['Previous 7 days',c=>{const d=new Date(c.createdAt.endsWith('Z')?c.createdAt:c.createdAt+'Z');return d<today&&d>=week;}],['Older',c=>new Date(c.createdAt.endsWith('Z')?c.createdAt:c.createdAt+'Z')<week]];
 $('#recent-chats').innerHTML=chats.length?groups.map(([label,match])=>{const rows=chats.filter(c=>(label==='Pinned'||!c.pinned)&&match(c));return rows.length?`<div class="history-group"><h4>${label}</h4>${rows.map(c=>`<div class="history-row ${c.pinned?'pinned':''} ${c.id===conversationId&&view==='chat'?'active':''}"><button class="history-chat" data-conversation="${c.id}" title="${esc(c.title)}">${esc(c.title)}</button>${c.pinned?`<span class="history-pin" aria-hidden="true">${icon('pin')}</span>`:''}<button class="history-more" data-chat-menu="${c.id}" aria-label="Options for ${esc(c.title)}" title="Chat options">···</button></div>`).join('')}</div>`:'';}).join(''):`<p class="sidebar-empty">${historyQuery?'No matching chats.':'No chats yet.'}</p>`;
 $$('[data-conversation]').forEach(b=>b.onclick=()=>{conversationId=b.dataset.conversation;navigate('chat');renderHistory();});
 $$('[data-chat-menu]').forEach(b=>b.onclick=()=>{menuChat=historyChats.find(c=>c.id===b.dataset.chatMenu);$('#pin-chat').textContent=menuChat.pinned?'Unpin chat':'Pin chat';const menu=$('#chat-menu');menu.hidePopover();const rect=b.getBoundingClientRect();menu.style.left=Math.min(innerWidth-190,rect.right+7)+'px';menu.style.top=Math.min(innerHeight-150,rect.top)+'px';menu.showPopover();});
}
$('#pin-chat').onclick=async e=>{const chat=menuChat;if(!chat)return;e.target.disabled=true;try{await api('/conversations/'+chat.id+'/pin',{method:'PUT',body:{pinned:!chat.pinned}});$('#chat-menu').hidePopover();await recentChats();toast(chat.pinned?'Chat unpinned.':'Chat pinned.');}catch(err){toast(err.message);}finally{e.target.disabled=false;}};
$('#search-chats').onclick=()=>{const field=$('#chat-history-search');field.classList.toggle('hidden');if(!field.classList.contains('hidden'))field.focus();else{field.value='';historyQuery='';renderHistory();}};
function expandChatSidebar(){ $('#app-view').classList.remove('sidebar-collapsed');$('#toggle-chat-sidebar').setAttribute('aria-expanded','true'); }
$('#rail-chat').onclick=expandChatSidebar;
$('#toggle-chat-sidebar').onclick=()=>{const collapsed=$('#app-view').classList.toggle('sidebar-collapsed');$('#toggle-chat-sidebar').setAttribute('aria-expanded',String(!collapsed));};

$('#chat-history-search').oninput=e=>{historyQuery=e.target.value;renderHistory();};
$('#mobile-chats').onclick=()=>{if(matchMedia('(max-width:760px)').matches)openMobileHistory();else expandChatSidebar();};
function openMobileHistory(){modal('<h2>Your chats</h2><label>Find a conversation<input id="mobile-history-query" type="search" placeholder="Search your chats…" /></label><div id="mobile-history-list"></div>');const render=query=>{const rows=historyChats.filter(c=>c.title.toLowerCase().includes(query.toLowerCase()));$('#mobile-history-list').innerHTML=rows.length?rows.map(c=>`<button class="mobile-history-chat" data-mobile-conversation="${c.id}">${esc(c.title)}</button>`).join(''):'<p class="muted">No matching chats.</p>';$$('[data-mobile-conversation]').forEach(b=>b.onclick=()=>{conversationId=b.dataset.mobileConversation;$('#modal').close();navigate('chat');renderHistory();});};render('');$('#mobile-history-query').oninput=e=>render(e.target.value);}
$('#rename-chat').onclick=()=>{const chat=menuChat;$('#chat-menu').hidePopover();if(!chat)return;modal(`<h2>Rename chat</h2><form id="rename-chat-form"><label>Chat title<input name="title" value="${esc(chat.title)}" maxlength="100" required /></label><p class="form-error" id="rename-error"></p><button class="primary wide">Save title</button></form>`);const field=$('#rename-chat-form input');field.focus();field.select();$('#rename-chat-form').onsubmit=async e=>{e.preventDefault();try{await api('/conversations/'+chat.id,{method:'PATCH',body:{title:field.value}});$('#modal').close();await recentChats();toast('Chat renamed.');}catch(err){$('#rename-error').textContent=err.message;}};};
$('#delete-chat').onclick=()=>{const chat=menuChat;$('#chat-menu').hidePopover();if(!chat)return;modal(`<h2>Delete this chat?</h2><p><strong>${esc(chat.title)}</strong></p><p>This permanently removes the conversation and its saved answers. This cannot be undone.</p><p class="form-error" id="delete-chat-error"></p><div class="dialog-actions"><button id="cancel-chat-delete" class="secondary">Cancel</button><button id="confirm-chat-delete" class="danger">Delete chat</button></div>`);$('#cancel-chat-delete').onclick=()=>$('#modal').close();$('#cancel-chat-delete').focus();$('#confirm-chat-delete').onclick=async e=>{e.target.disabled=true;try{await api('/conversations/'+chat.id,{method:'DELETE'});$('#modal').close();if(conversationId===chat.id){conversationId=null;await navigate('chat');}await recentChats();toast('Chat deleted.');}catch(err){$('#delete-chat-error').textContent=err.message;e.target.disabled=false;}};};
const prompts=[{icon:'layers',title:'Understand the bigger picture',body:'How is the platform architecture organized?',query:'Explain the platform architecture and the responsibilities of each major service.'},{icon:'shield',title:'Find the details that matter',body:'How are authentication and access managed?',query:'How does the system handle authentication, access control and sensitive actions?'},{icon:'spark',title:'Turn knowledge into clarity',body:'Walk me through retrieval and model routing.',query:'Explain the complete RAG pipeline, embedding, reranking and model failover.'}];
async function renderHome(){
 activateLiveView('overview',renderOverviewData);
}
function renderOverviewData(d){
 if(!user||view!=='home')return;
 main.innerHTML=`<section>${pageActions(`<button class="primary" id="home-upload">${icon('upload')} Add knowledge</button>`)}
 <div class="stats-grid">${[{label:'Knowledge documents',number:d.documents,note:d.ready+' ready',icon:'files'},{label:'Searchable passages',number:d.chunks.toLocaleString(),note:'',icon:'layers'},{label:'Conversations',number:d.conversations,note:'',icon:'spark'},{label:'AI usage estimate',number:money(d.costUsd),note:d.tokens.toLocaleString()+' tokens',icon:'activity'}].map(s=>`<div class="stat-card"><div class="stat-top"><span>${s.label}</span><span class="stat-icon">${icon(s.icon)}</span></div><div class="stat-number">${s.number}</div>${s.note?`<div class="stat-note">${s.note}</div>`:''}</div>`).join('')}</div>
 <div class="section-header"><div><h3>Recently added knowledge</h3></div><button data-view="documents">View library ↗</button></div><div class="table-wrap">${d.recent.length?docTable(d.recent,false):empty('No documents')}</div></section>`;
 $('#home-upload').onclick=showUpload;bindDocs();
}
function docTable(docs,manage=true){return `<table class="document-table"><thead><tr><th>DOCUMENT NAME</th><th>STATUS</th><th>VISIBILITY</th><th>PASSAGES</th><th>${manage?'ACTIONS':'ADDED'}</th></tr></thead><tbody>${docs.map(d=>`<tr><td><button class="doc-name" data-doc="${d.id}" title="${esc(d.title)}"><span class="file-badge">${esc((d.fileName?.split('.').pop()||'DOC').toUpperCase().slice(0,4))}</span><span>${esc(d.title)}</span></button></td><td><button class="status-button" data-status="${d.id}" title="View indexing details">${badge(d.status)}</button></td><td>${esc(d.visibility)}</td><td>${d.chunkCount}</td><td>${manage&&d.canManage?`<div class="table-actions"><button data-reindex="${d.id}" ${d.status==='Indexing'?'disabled':''} title="Retry indexing with the current embedding model">Reindex</button><button data-delete="${d.id}">Delete</button></div>`:date(d.createdAt)}</td></tr>`).join('')}</tbody></table>`;}
function bindDocs(){
 $$('[data-doc]').forEach(b=>b.onclick=()=>showDocument(b.dataset.doc));
 $$('[data-status]').forEach(b=>b.onclick=async()=>{try{const docs=await api('/documents');const d=docs.find(d=>d.id===b.dataset.status);if(d)modal(`<h2>${esc(d.title)}</h2><p>${badge(d.status)} · ${d.chunkCount} searchable passages</p><div class="section-note">${esc(d.error||({'Ready':'This document is ready for semantic and keyword search.','Indexing':'The document worker is preparing this document. This page refreshes automatically.','Queued':'This document is queued for the background worker.','LexicalOnly':'Keyword search is available. Reindex to retry semantic search.'}[d.status]||'Reindex this document to retry processing.'))}</div>`);}catch(e){toast(e.message);}});
 $$('[data-reindex]').forEach(b=>b.onclick=async()=>{try{b.disabled=true;await api(`/documents/${b.dataset.reindex}/reindex`,{method:'POST'});toast('Document queued for reindexing.');await renderDocuments();}catch(e){toast(e.message);b.disabled=false;}});
 $$('[data-delete]').forEach(b=>b.onclick=async()=>{try{await api(`/documents/${b.dataset.delete}/delete-request`,{method:'POST'});toast('Deletion requested.');if(user.role==='Admin')await navigate('approvals');}catch(e){toast(e.message);}});
}
async function showDocument(id){
 try{const d=await api('/documents/'+id);modal(`<h2>${esc(d.title)}</h2><p>${esc(d.fileName)} · ${esc(d.status)}</p><a class="secondary" href="/api/documents/${id}/download">Download original ${icon('arrow')}</a>${d.chunks.length?d.chunks.map(c=>`<div class="source-section">${esc(c.section)}${c.page?' · Page '+c.page:''}</div><div class="source-preview" dir="auto">${esc(c.text)}</div>`).join(''):'<p class="empty">This document is waiting to be indexed.</p>'}`);}catch(e){toast(e.message);}
}
function showUpload(){
 modal(`<h2>Add knowledge</h2><form id="upload-form"><div class="file-drop">${icon('upload')}<p>Choose a document or image</p><input type="file" name="file" accept=".pdf,.md,.txt,.csv,.png,.jpg,.jpeg,.webp" required/><p>PDF, Markdown, text, CSV or image · Up to 20 MB</p></div><label>Document title<input name="title" placeholder="Optional — use the file name" maxlength="180"/></label>${user.role==='Admin'?`<label>Team<input name="department" value="${esc(docFolderTeam||user.department)}" required maxlength="100" /></label>`:''}<label>Access<select name="visibility"><option value="Organization">Everyone in the organization</option><option value="Department">My team</option><option value="Private">Only me and workspace administrators</option></select></label><p class="form-error" id="upload-error"></p><button class="primary wide" type="submit">Add knowledge ${icon('arrow')}</button></form>`);
 if(docFolderAccess)$('#upload-form select[name=visibility]').value=docFolderAccess;
 $('#upload-form').onsubmit=async e=>{e.preventDefault();const b=$('button[type=submit]',e.target);b.disabled=true;b.textContent='Uploading…';try{await api('/documents',{method:'POST',body:new FormData(e.target)});$('#modal').close();toast('Document added. Atlas is preparing it for search.');await navigate('documents');}catch(err){$('#upload-error').textContent=err.message;b.disabled=false;b.textContent='Add knowledge';}};
}
async function renderDocuments(){
 activateLiveView('documents',renderDocumentData);
}
function renderDocumentData(data){
 if(!user||view!=='documents')return;documents=data;
 const focused=document.activeElement?.id==='filter-docs';const selection=focused?[$('#filter-docs').selectionStart,$('#filter-docs').selectionEnd]:null;const scroll=main.scrollTop;
 main.innerHTML=`<section>${pageActions(`<button id="add-document" class="primary">${icon('upload')} Add knowledge</button>`)}<div class="toolbar"><input id="filter-docs" placeholder="Find a document…" aria-label="Filter documents"/><span class="badge">${documents.length} documents · ${documents.reduce((n,d)=>n+d.chunkCount,0).toLocaleString()} passages</span><button id="refresh-docs" class="secondary">Refresh</button></div><nav id="document-breadcrumbs" class="document-breadcrumbs" aria-label="Document folders"></nav><div id="doc-list"></div></section>`;
 $('#add-document').onclick=showUpload;$('#refresh-docs').onclick=renderDocuments;
 $('#filter-docs').value=docFilter;$('#filter-docs').oninput=e=>{docFilter=e.target.value;filterDocs();};
 $('#refresh-docs').insertAdjacentHTML('beforebegin','<select id="filter-state" aria-label="Filter by indexing status"><option value="all">All statuses</option><option value="Ready">Ready</option><option value="Indexing">Processing</option><option value="Failed">Needs attention</option></select>');$('#filter-state').value=docState;$('#filter-state').onchange=e=>{docState=e.target.value;filterDocs();};filterDocs();
 if(focused){$('#filter-docs').focus();$('#filter-docs').setSelectionRange(...selection);}main.scrollTop=scroll;
}
function filterDocs(){
 const filtered=documents.filter(d=>d.title.toLowerCase().includes(docFilter.toLowerCase())&&(docState==='all'||(docState==='Indexing'?['Indexing','Queued'].includes(d.status):docState==='Failed'?['Failed','LexicalOnly'].includes(d.status):d.status===docState)));
 const scoped=filtered.filter(d=>(!docFolderTeam||d.department===docFolderTeam)&&(!docFolderAccess||d.visibility===docFolderAccess));
 $('#document-breadcrumbs').innerHTML='<button id="all-document-teams">All teams</button>'+(docFolderTeam?' <span>/</span> <button id="current-document-team">'+esc(docFolderTeam)+'</button>':'')+(docFolderAccess?' <span>/</span> <span aria-current="page">'+esc(accessName(docFolderAccess))+'</span>':'');
 $('#all-document-teams').onclick=()=>{docFolderTeam=null;docFolderAccess=null;filterDocs();};if($('#current-document-team'))$('#current-document-team').onclick=()=>{docFolderAccess=null;filterDocs();};
 let folders=[];
 if(!docFilter&&!docFolderTeam){const teams=[...new Set([...filtered.map(d=>d.department),user.department])].sort((a,b)=>a.localeCompare(b));folders=teams.map(team=>({label:team,kind:'team',value:team,count:filtered.filter(d=>d.department===team).length}));}
 else if(!docFilter&&!docFolderAccess)folders=['Organization','Department','Private'].filter(access=>scoped.some(d=>d.visibility===access)).map(access=>({label:accessName(access),kind:'access',value:access,count:scoped.filter(d=>d.visibility===access).length}));
 if(folders.length){$('#doc-list').innerHTML='<div class="folder-grid">'+folders.map((folder,index)=>'<button class="folder-card" data-folder="'+index+'">'+icon('folder')+'<span>'+esc(folder.label)+'</span><small>'+folder.count+'</small>'+icon('arrow')+'</button>').join('')+'</div>';$$('[data-folder]').forEach(b=>b.onclick=()=>{const folder=folders[Number(b.dataset.folder)];if(folder.kind==='team'){docFolderTeam=folder.value;docFolderAccess=null;}else docFolderAccess=folder.value;filterDocs();});}
 else $('#doc-list').innerHTML=scoped.length?'<div class="table-wrap">'+docTable(scoped)+'</div>':empty('No documents');
 bindDocs();
}

function bindPrompts(){ $$('[data-prompt]').forEach(b=>b.onclick=async()=>{conversationId=null;await navigate('chat');$('#chat-input').value=prompts[Number(b.dataset.prompt)].query;$('#chat-input').focus();}); }
function markdown(text,citations=sourceList,messageId='live'){
 const inline=s=>esc(s).replace(/`([^`]+)`/g,'<code>$1</code>').replace(/\*\*(.+?)\*\*/g,'<strong>$1</strong>').replace(/\[(\d+(?:\s*[,;]\s*\d+)*)\]/g,(_,group)=>group.split(/[,;]/).map(value=>{const n=Number(value.trim());return n>0&&n<=citations.length?`<button class="citation" data-citation="${n-1}" data-citation-message="${esc(messageId)}" title="View source ${n}">${n}</button>`:`[${n}]`;}).join(' '));
 const lines=String(text).split('\n');let output='',paragraph=[],list=null,code=null;
 const flush=()=>{if(paragraph.length){output+='<p>'+inline(paragraph.join(' '))+'</p>';paragraph=[];}if(list){output+=`</${list}>`;list=null;}};
 const cells=line=>line.trim().replace(/^\||\|$/g,'').split('|').map(x=>x.trim());
 for(let i=0;i<lines.length;i++){
  const line=lines[i];
  if(line.trim().startsWith('```')){flush();if(code!==null){output+='<pre><code>'+esc(code.join('\n'))+'</code></pre>';code=null;}else code=[];continue;}
  if(code!==null){code.push(line);continue;}
  if(!line.trim()){flush();continue;}
  const h=line.match(/^(#{1,6})\s+(.+)$/);if(h){flush();output+=`<${h[1].length<3?'h3':'h4'}>${inline(h[2])}</${h[1].length<3?'h3':'h4'}>`;continue;}
  if(line.includes('|')&&i+1<lines.length&&/^\s*\|?\s*:?-{3,}/.test(lines[i+1])){
   flush();const headers=cells(line);output+='<div class="markdown-table"><table><thead><tr>'+headers.map(c=>'<th>'+inline(c)+'</th>').join('')+'</tr></thead><tbody>';i+=2;
   while(i<lines.length&&lines[i].includes('|')&&lines[i].trim()){output+='<tr>'+cells(lines[i]).map(c=>'<td>'+inline(c)+'</td>').join('')+'</tr>';i++;}i--;output+='</tbody></table></div>';continue;
  }
  const item=line.match(/^\s*(?:([-*])|\d+[.)])\s+(.+)$/);if(item){const type=item[1]?'ul':'ol';if(list!==type){flush();output+=`<${type}>`;list=type;}output+='<li>'+inline(item[2])+'</li>';continue;}
  if(line.startsWith('> ')){flush();output+='<blockquote>'+inline(line.slice(2))+'</blockquote>';continue;}
  if(list)flush();paragraph.push(line);
 }
 flush();if(code!==null)output+='<pre><code>'+esc(code.join('\n'))+'</code></pre>';return output;
}
async function renderChat(){
 eventSource?.close();eventSource=null;runId=null;sourceList=[];imageDocumentId=null;lastMessages=[];
 main.innerHTML=`<section class="chat-layout ${(!conversationId||matchMedia('(max-width:760px)').matches)?'sources-collapsed':''}"><div class="chat-main"><div class="chat-top"><div><h2>Ask Atlas</h2><p>Answers from your knowledge, with sources.</p></div><div class="chat-top-actions"><button type="button" id="chat-new" class="secondary" title="Start a new conversation" aria-label="Start a new conversation">+ <span class="new-conversation-label">New chat</span></button><button type="button" id="toggle-sources" class="secondary" aria-label="Show or hide answer sources" aria-expanded="${conversationId&&!matchMedia('(max-width:760px)').matches?true:false}" aria-controls="sources-panel" title="Show or hide sources">${icon('files')} <span class="sources-toggle-label">Sources</span><span id="source-count">0</span></button></div></div><div id="messages" class="messages" aria-label="Conversation messages" tabindex="0"></div><button type="button" id="scroll-latest" class="scroll-latest hidden">↓ Latest message</button><div id="run-stage" class="run-stage" role="status"></div><form id="chat-form" class="composer"><div id="attachment" class="hidden attachment-chip"></div><textarea id="chat-input" placeholder="Ask a question about your organization…" rows="2" maxlength="6000" aria-label="Your question" required></textarea><div class="composer-bottom"><div class="composer-tools"><button type="button" id="attach-image" class="icon-button" title="Attach an image" aria-label="Attach an image">${icon('attach')}</button><button type="button" id="record-audio" class="icon-button" title="Dictate a question" aria-label="Dictate a question">${icon('mic')}</button><span id="input-hint">Enter to send · Shift + Enter for a new line</span></div><button id="send-chat" class="send-button" type="submit">Ask Atlas ${icon('send')}</button><button id="stop-chat" type="button" class="danger hidden">Stop answer</button></div></form><p class="composer-note">AI can make mistakes.</p></div><aside id="sources-panel" class="sources-panel" aria-label="Answer sources"><h3>Sources for this answer</h3><div id="source-list" tabindex="0" aria-label="Scrollable source passages"></div></aside></section>`;
 $('#view-actions').append($('#chat-new'),$('#toggle-sources'));$('.chat-top').remove();
 renderSources();
 if(conversationId){
  lastMessages=await api('/conversations/'+conversationId);if(view!=='chat')return;
  $('#messages').innerHTML=lastMessages.map(m=>messageHTML(m.role,m.text,m.id,JSON.parse(m.sources||'[]'))).join('');
  const last=lastMessages.filter(m=>m.role==='assistant').at(-1);if(last){sourceList=JSON.parse(last.sources||'[]');renderSources();refreshMarkdown();}
  if(view!=='chat')return;const chat=historyChats.find(c=>c.id===conversationId);
  if(chat?.activeRunId){startAnswer();listen(chat.activeRunId);}
 }else $('#messages').innerHTML=`<div class="chat-empty"><h2>What can I help with?</h2><div class="chat-prompts">${prompts.map((p,i)=>`<button data-prompt="${i}">${p.body}</button>`).join('')}</div></div>`;
 $('#chat-form').onsubmit=sendChat;$('#chat-input').onkeydown=e=>{if(e.key==='Enter'&&!e.shiftKey){e.preventDefault();if(!runId)$('#chat-form').requestSubmit();}};
 $('#chat-new').onclick=()=>{conversationId=null;navigate('chat');recentChats();};
 $('#toggle-sources').onclick=()=>{const collapsed=$('.chat-layout').classList.toggle('sources-collapsed');$('#toggle-sources').setAttribute('aria-expanded',String(!collapsed));};
 $('#messages').onscroll=()=>$('#scroll-latest').classList.toggle('hidden',isAtBottom());$('#scroll-latest').onclick=()=>scrollMessages();
 $('#chat-input').oninput=e=>{e.target.style.height='auto';e.target.style.height=Math.min(150,e.target.scrollHeight)+'px';};
 $('#stop-chat').onclick=async()=>{try{await api(`/runs/${runId}/stop`,{method:'POST'});}catch(e){toast(e.message);}};
 $('#attach-image').onclick=()=>$('#image-input').click();$('#record-audio').onclick=record;bindPrompts();bindMessageActions();scrollMessages();
}
function messageHTML(role,text,id,sources=[]){return role==='user'?`<div class="message user">${esc(text)}</div>`:`<div class="message assistant" data-message="${id}"><div class="assistant-label"><span class="brand-mark">A</span>ATLAS</div><div class="message-content">${markdown(text,sources,id)}</div><div class="message-actions"><button data-speak="${id}">${icon('volume')} Listen</button><button data-copy="${id}">${icon('copy')} Copy</button></div></div>`;}
function refreshMarkdown(){lastMessages.filter(m=>m.role==='assistant').forEach(m=>{const el=$(`[data-message="${m.id}"] .message-content`);if(el)el.innerHTML=markdown(m.text,JSON.parse(m.sources||'[]'),m.id);});}
function startAnswer(){
 answerText='';lastRunError='';$('#messages .chat-empty')?.remove();$('#messages').insertAdjacentHTML('beforeend',messageHTML('assistant','','live'));answerEl=$('[data-message="live"] .message-content');
 $('#send-chat').classList.add('hidden');$('#stop-chat').classList.remove('hidden');$('#run-stage').classList.add('running');
}
async function sendChat(e){
 e.preventDefault();if(runId)return;const input=$('#chat-input');const message=input.value.trim();if(!message)return;
 const b=$('#send-chat');b.disabled=true;
 try{const d=await api('/chat',{method:'POST',body:{message,conversationId,imageDocumentId}});conversationId=d.conversationId;lastQuestion=message;saveRoute(true);
 $('#messages .chat-empty')?.remove();$('#messages').insertAdjacentHTML('beforeend',messageHTML('user',message,''));input.value='';imageDocumentId=null;$('#attachment').classList.add('hidden');
 input.style.height='';startAnswer();scrollMessages();listen(d.runId);input.focus();await recentChats();}catch(err){toast(err.message);}finally{b.disabled=false;}
}
function listen(id){
 runId=id;eventSource?.close();eventSource=new EventSource(`/api/runs/${id}/events`);
 const source=eventSource;let lastEventId=0;
 eventSource.onmessage=e=>{if(!user||eventSource!==source)return;const data=JSON.parse(e.data);
  if(data.type==='subscription_closed'){source.close();if(data.reason==='session_revoked'){showLogin();return;}runId=null;eventSource=null;$('#run-stage').textContent='This answer is no longer available.';$('#send-chat').classList.remove('hidden');$('#stop-chat').classList.add('hidden');return;}
  if(e.lastEventId){const cursor=Number(e.lastEventId);if(!Number.isSafeInteger(cursor)||cursor<=lastEventId)return;lastEventId=cursor;}
  if(data.type==='stage')$('#run-stage').textContent=data.message;
  if(data.type==='sources'){sourceList=data.sources;renderSources();}
  if(data.type==='text_delta'){const follow=isAtBottom();answerText+=data.delta;answerEl.innerHTML=markdown(answerText);if(follow)scrollMessages();else $('#scroll-latest').classList.remove('hidden');}
  if(data.type==='tool_call')$('#run-stage').textContent='Using '+data.name.replaceAll('_',' ');
  if(data.type==='approval_pending')toast('Atlas proposed an action. Review it in Approvals.');
  if(data.type==='error'){lastRunError=data.message;toast(data.message);$('#run-stage').textContent=data.message;}
  if(data.type==='run_cancelled')$('#run-stage').textContent=data.message;
  if(data.type==='run_finished'){
   eventSource.close();eventSource=null;runId=null;sourceList=data.sources||sourceList;renderSources();$('#run-stage').classList.remove('running');
   if(data.status==='Succeeded')$('#run-stage').textContent='Answer complete · '+sourceList.length+' source passages';else if(!answerText)answerEl.innerHTML=`<div class="error-box">${esc(lastRunError||(data.status==='Cancelled'?'This answer was stopped. You can send your question again.':'The answer could not be completed. Please try again.'))}</div>`;
   $('#send-chat').classList.remove('hidden');$('#stop-chat').classList.add('hidden');
   const el=$('[data-message="live"]');if(el){const key='run-'+id;el.dataset.message=key;answerEl.innerHTML=markdown(answerText,sourceList,key);$$('[data-speak],[data-copy]',el).forEach(b=>{if(b.dataset.speak)b.dataset.speak=key;if(b.dataset.copy)b.dataset.copy=key;});lastMessages.push({id:key,text:answerText,role:'assistant',sources:JSON.stringify(sourceList)});}bindMessageActions();recentChats();
  }
 };
 eventSource.onerror=async()=>{if(eventSource!==source||!user)return;if(view==='chat')$('#run-stage').textContent='Reconnecting to your answer…';try{const valid=await verifyLiveSession();if(eventSource!==source||!user)return;if(!valid)showLogin();}catch{}};
}
function isAtBottom(){const el=$('#messages');return !el||el.scrollHeight-el.scrollTop-el.clientHeight<85;}
function scrollMessages(){const el=$('#messages');if(el)el.scrollTop=el.scrollHeight;$('#scroll-latest')?.classList.add('hidden');}
function renderSources(){const el=$('#source-list');if(!el)return;if($('#source-count'))$('#source-count').textContent=sourceList.length;el.innerHTML=sourceList.length?sourceList.map((s,i)=>`<button class="source-card" data-source="${i}"><span class="source-number">${i+1}</span><strong>${esc(s.title)}</strong><small>${esc(s.section)}${s.page?' · p. '+s.page:''}</small><p dir="auto">${esc(s.excerpt)}</p></button>`).join(''):`<div class="source-empty">${icon('book')}<p>No sources</p></div>`;$$('[data-source]').forEach(b=>b.onclick=()=>showSource(Number(b.dataset.source)));}
function showSource(i,sources=sourceList){const s=sources[i];if(!s)return;modal(`<h2>${esc(s.title)}</h2><p>${esc(s.section)}${s.page?' · Page '+s.page:''}</p><div class="source-preview" dir="auto">${esc(s.excerpt)}</div><button id="full-source" class="secondary">Open full document ${icon('arrow')}</button>`);$('#full-source').onclick=()=>{$('#modal').close();showDocument(s.documentId);};}
document.addEventListener('click',e=>{const b=e.target.closest('[data-citation]');if(b){const message=lastMessages.find(m=>String(m.id)===b.dataset.citationMessage);showSource(Number(b.dataset.citation),message?JSON.parse(message.sources||'[]'):sourceList);}});
function bindMessageActions(){
 $$('[data-copy]').forEach(b=>b.onclick=async()=>{const m=lastMessages.find(m=>String(m.id)===b.dataset.copy);if(m){try{await navigator.clipboard.writeText(m.text);toast('Answer copied.');}catch{toast('Clipboard access is unavailable. Select the answer to copy it.');}}});
 $$('[data-speak]').forEach(b=>b.onclick=async()=>{const m=lastMessages.find(m=>String(m.id)===b.dataset.speak);if(!m)return;b.disabled=true;try{const text=m.text.replace(/[#*`]/g,'').replace(/\[\d+(?:\s*[,;]\s*\d+)*\]/g,'').slice(0,1800);const blob=await api('/speech/synthesize',{method:'POST',body:{text,conversationId},binary:true});const url=URL.createObjectURL(blob);const audio=new Audio(url);activeSpeech?.pause();activeSpeech=audio;audio.onended=()=>URL.revokeObjectURL(url);await audio.play();}catch(e){toast(e.message);}finally{b.disabled=false;}});
}
$('#image-input').onchange=async e=>{const file=e.target.files[0];if(!file)return;try{const data=new FormData();data.append('file',file);data.append('visibility','Private');const doc=await api('/documents',{method:'POST',body:data});imageDocumentId=doc.id;$('#attachment').classList.remove('hidden');$('#attachment').innerHTML='<span>Image: '+esc(file.name)+'</span><button type="button" class="icon-button" id="remove-attachment" aria-label="Remove attached image">×</button>';$('#remove-attachment').onclick=()=>{imageDocumentId=null;$('#attachment').classList.add('hidden');};toast('Image ready for your next question.');}catch(err){toast(err.message);}e.target.value='';};
async function record(){
 if(recorder?.state==='recording'){recorder.stop();return;}
 if(!navigator.mediaDevices?.getUserMedia){toast('Microphone recording is unavailable in this browser.');return;}
 try{recordingStream=await navigator.mediaDevices.getUserMedia({audio:true});const mime=MediaRecorder.isTypeSupported('audio/webm')?'audio/webm':'audio/mp4';recorder=new MediaRecorder(recordingStream,{mimeType:mime});const parts=[];recorder.ondataavailable=e=>parts.push(e.data);recorder.onstop=async()=>{clearTimeout(recordingTimeout);recordingStream.getTracks().forEach(t=>t.stop());$('#record-audio')?.classList.remove('recording');if($('#input-hint'))$('#input-hint').textContent='Transcribing…';try{const data=new FormData();data.append('file',new Blob(parts,{type:mime}),mime.endsWith('webm')?'recording.webm':'recording.mp4');if(conversationId)data.append('conversationId',conversationId);const result=await api('/speech/transcribe',{method:'POST',body:data});if($('#chat-input')){$('#chat-input').value+=($('#chat-input').value?' ':'')+result.transcript;$('#chat-input').focus();}toast(result.transcript?'Transcript ready. Review it before sending.':'No speech detected. Try again.');}catch(e){toast(e.message);}finally{if($('#input-hint'))$('#input-hint').textContent='Review your transcript, then ask Atlas.';}};recorder.start();$('#record-audio').classList.add('recording');$('#input-hint').textContent='Recording · click microphone to finish';recordingTimeout=setTimeout(()=>{if(recorder.state==='recording')recorder.stop();},60000);}catch(e){toast('Microphone access was unavailable. Check browser permissions.');}
}
function renderSearch(){
 main.innerHTML=`<section><form id="search-form" class="search-form"><input id="search-query" placeholder="Search by topic, phrase, or a question…" aria-label="Search knowledge" maxlength="1200" required/><button class="primary">${icon('search')} Search knowledge</button></form><div id="search-results" class="results">${''}</div></section>`;
 $('#search-query').focus();$('#search-form').onsubmit=async e=>{e.preventDefault();const b=$('button',e.target);b.disabled=true;$('#search-results').innerHTML='<div class="skeleton">Finding and reranking your sources…</div>';try{const results=await api('/search',{method:'POST',body:{query:$('#search-query').value}});$('#search-results').innerHTML=results.length?results.map((s,i)=>`<article class="result-card"><div class="result-meta"><span>PASSAGE ${i+1}</span><span>${esc(s.section)}</span>${s.page?`<span>Page ${s.page}</span>`:''}</div><h3>${esc(s.title)}</h3><p dir="auto">${esc(s.excerpt)}</p><button class="secondary" data-doc="${s.documentId}">Explore document ↗</button></article>`).join(''):empty('No results');bindDocs();}catch(err){$('#search-results').innerHTML=`<div class="error-box">${esc(err.message)}</div>`;}finally{b.disabled=false;}};
}
async function renderApprovals(){
 const data=await api('/approvals');if(view!=='approvals')return;
 main.innerHTML=`<section><div class="results">${data.length?data.map(a=>`<article class="approval-card"><div><h3>${esc(a.title||'Document already removed')}</h3><p>Requested ${date(a.createdAt)} · ${a.id.slice(0,8)}</p></div><div>${a.status==='Pending'?`<button class="secondary" data-approval="${a.id}" data-decision="false">Keep document</button><button class="danger" data-approval="${a.id}" data-decision="true">Delete document</button>`:badge(a.status)}</div></article>`).join(''):empty('No pending approvals')}</div></section>`;
 $$('[data-approval]').forEach(b=>b.onclick=async()=>{b.disabled=true;try{await api('/approvals/'+b.dataset.approval,{method:'POST',body:{approve:b.dataset.decision==='true'}});toast(b.dataset.decision==='true'?'Document deleted.':'Document kept.');await renderApprovals();}catch(e){toast(e.message);b.disabled=false;}});
}
async function renderSystem(){
 const [s,models,usage]=await Promise.all([api('/admin/system'),api('/models'),api('/admin/usage')]);if(view!=='system')return;
 const spent=usage.reduce((n,u)=>n+u.costUsd,0);main.innerHTML=`<section>${pageActions(`<button id="refresh-system" class="secondary">Refresh status</button>`)}<div class="system-grid">${s.services.map(x=>`<div class="system-card" title="${esc(x.detail)}"><h3>${esc(x.name)}</h3>${badge(x.status)}${x.status==='healthy'?'':`<p>${esc(x.detail)}</p>`}</div>`).join('')}</div><div class="section-header model-table"><h3>Models</h3><span class="badge">OpenRouter + Deepgram</span></div><div class="table-wrap"><table class="document-table"><thead><tr><th>ROLE</th><th>MODEL</th><th>ACCESS</th></tr></thead><tbody>${models.map(m=>`<tr><td>${esc(m.role)}</td><td>${esc(m.model)}</td><td>${badge(m.enabled?'Enabled':'Disabled')}</td></tr>`).join('')}</tbody></table></div><div class="section-header model-table"><h3>Recent AI usage</h3><span class="badge">${money(spent)} · ${usage.length} recent calls</span></div><div class="table-wrap"><table class="document-table"><thead><tr><th>ROLE / MODEL</th><th>INPUT</th><th>OUTPUT</th><th>COST</th><th>STATUS</th></tr></thead><tbody>${usage.slice(0,30).map(u=>`<tr><td>${esc(u.role)}<br/><small>${esc(u.model)}</small></td><td>${u.inputTokens}</td><td>${u.outputTokens}</td><td>${money(u.costUsd)}${u.costReported?'':' *'}</td><td>${badge(u.status)}</td></tr>`).join('')}</tbody></table></div><p class="composer-note">* Estimated cost. Speech billed separately.</p></section>`;$('#refresh-system').onclick=renderSystem;
}
async function renderTeam(){
 const data=await api('/admin/users');if(view!=='team')return;
 main.innerHTML=`<section>${pageActions(`<button id="add-user" class="primary">${icon('users')} Add team member</button>`)}<div class="table-wrap"><table class="document-table"><thead><tr><th>TEAM MEMBER</th><th>ROLE</th><th>TEAM</th><th>ACCESS</th><th>ACTION</th></tr></thead><tbody>${data.map(u=>`<tr><td>${esc(u.name)}<br/><small>${esc(u.email)}</small></td><td>${esc(u.role)}</td><td>${esc(u.department)}</td><td>${badge(u.enabled?'Enabled':'Disabled')}</td><td>${u.id!==user.id?`<button class="small-link" data-user="${u.id}" data-enable="${!u.enabled}">${u.enabled?'Disable':'Enable'}</button>`:''}</td></tr>`).join('')}</tbody></table></div></section>`;
 $('#add-user').onclick=()=>{modal(`<h2>Add team member</h2><form id="user-form"><label>Full name<input name="name" required maxlength="100"/></label><label>Work email<input type="email" name="email" required/></label><label>Temporary password<input type="password" name="password" required minlength="12" maxlength="128" autocomplete="new-password"/></label><div class="modal-fields"><label>Team<input name="department" value="General" required maxlength="100"/></label><label>Role<select name="role"><option>Member</option><option>Admin</option></select></label></div><p id="user-error" class="form-error"></p><button class="primary wide">Create account</button></form>`);$('#user-form').onsubmit=async e=>{e.preventDefault();const b=$('button',e.target);b.disabled=true;try{await api('/admin/users',{method:'POST',body:Object.fromEntries(new FormData(e.target))});$('#modal').close();toast('Team member added.');await renderTeam();}catch(err){$('#user-error').textContent=err.message;b.disabled=false;}};};
 $$('[data-user]').forEach(b=>b.onclick=async()=>{try{await api(`/admin/users/${b.dataset.user}/enabled`,{method:'POST',body:{enabled:b.dataset.enable==='true'}});await renderTeam();}catch(e){toast(e.message);}});
}
function renderSettings(){
 main.innerHTML=`<section><div class="settings-card"><h3>${esc(user.name)}</h3><p>${esc(user.email)} · ${esc(user.department)} · ${esc(user.role)}</p><hr class="muted"/><h3>Change your password</h3><form id="password-form"><label>Current password<input name="currentPassword" type="password" required autocomplete="current-password"/></label><label>New password<input name="newPassword" type="password" minlength="12" maxlength="128" required autocomplete="new-password"/></label><p class="muted">At least 12 characters. Signs out all sessions.</p><p id="password-error" class="form-error"></p><button class="primary">Update password</button></form><div class="tabs"><button id="account-logout">Sign out</button></div></div></section>`;
 $('#password-form').onsubmit=async e=>{e.preventDefault();try{await api('/auth/password',{method:'POST',body:Object.fromEntries(new FormData(e.target))});showLogin();await token();toast('Password updated. Sign in with your new password.');}catch(err){$('#password-error').textContent=err.message;}};$('#account-logout').onclick=()=>$('#logout').click();
}
await boot();
