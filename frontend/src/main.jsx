import React, { useEffect, useRef, useState } from 'react';
import { createRoot } from 'react-dom/client';
import { LayoutDashboard, ListTodo, BarChart3, Users, ArrowUpRight, Plus, Search, SlidersHorizontal, ChevronLeft, ChevronRight, X, Clock3, CalendarDays, ArrowRight, Check, Circle, AlertCircle, Download, Layers3, PanelLeftClose } from 'lucide-react';
import './style.css';

const statuses = { New: 'Новая', InProgress: 'В работе', Done: 'Завершена' };
const priorities = { High: 'Высокий', Medium: 'Средний', Low: 'Низкий' };
const date = value => value ? new Date(value + (value.length === 10 ? 'T12:00:00' : '')).toLocaleDateString('ru-RU', { day: 'numeric', month: 'short' }) : 'Без срока';

async function api(path, options = {}) {
  const response = await fetch(`/api${path}`, { ...options, headers: { 'Content-Type': 'application/json', ...options.headers } });
  if (!response.ok) {
    const problem = await response.json().catch(() => ({}));
    throw new Error(problem.title || `Не удалось выполнить запрос (${response.status}).`);
  }
  return response.json();
}

function App() {
  const [view, setView] = useState('board');
  const [team, setTeam] = useState([]), [summary, setSummary] = useState(null);
  const [data, setData] = useState({ items: [], total: 0, totalPages: 0 });
  const [loading, setLoading] = useState(true), [error, setError] = useState('');
  const [search, setSearch] = useState(''), [query, setQuery] = useState('');
  const [status, setStatus] = useState(''), [priority, setPriority] = useState(''), [assignee, setAssignee] = useState('');
  const [overdue, setOverdue] = useState(false), [sort, setSort] = useState('newest'), [page, setPage] = useState(1);
  const [revision, setRevision] = useState(0), [selected, setSelected] = useState(null), [creating, setCreating] = useState(false);
  const [filters, setFilters] = useState(false), [mobileNav, setMobileNav] = useState(false);
  const [notice, setNotice] = useState('');
  useEffect(() => { const timer = setTimeout(() => setQuery(search.trim()), 250); return () => clearTimeout(timer); }, [search]);
  useEffect(() => { setPage(1); }, [query, status, priority, assignee, overdue, sort]);
  useEffect(() => {
    const controller = new AbortController();
    api('/workspace', { signal: controller.signal })
      .then(({team, summary}) => { setTeam(team); setSummary(summary); }).catch(e => { if (e.name !== 'AbortError') setError(e.message); });
    return () => controller.abort();
  }, [revision]);
  useEffect(() => {
    const controller = new AbortController(); setLoading(true); setError('');
    const p = new URLSearchParams({ search: query, sort, page, pageSize: 12, overdue });
    if (status) p.set('status', status); if (priority) p.set('priority', priority); if (assignee) p.set('assigneeId', assignee);
    api(`/items?${p}`, { signal: controller.signal }).then(setData).catch(e => { if (e.name !== 'AbortError') setError(e.message); })
      .finally(() => { if (!controller.signal.aborted) setLoading(false); });
    return () => controller.abort();
  }, [query, status, priority, assignee, overdue, sort, page, revision]);
  const refresh = message => { setRevision(x => x + 1); setNotice(message); };
  useEffect(() => { if (!notice) return; const timer = setTimeout(() => setNotice(''), 4500); return () => clearTimeout(timer); }, [notice]);
  const navigate = next => { setView(next); setMobileNav(false); };
  const hasFilters = query || status || priority || assignee || overdue;
  const clearFilters = () => { setSearch(''); setStatus(''); setPriority(''); setAssignee(''); setOverdue(false); };
  return <div className="shell">
    <a className="skip" href="#main">К содержимому</a>
    <aside className={mobileNav ? 'sidebar open' : 'sidebar'}>
      <a href="#" className="brand" onClick={e => { e.preventDefault(); navigate('board'); }}><span className="logo"><Layers3 size={23}/></span>taskflow<span className="brand-dot">.</span></a>
      <button className="workspace" onClick={() => navigate('team')}><span className="workspace-icon">I</span><span>Idea to Action<small>Рабочее пространство</small></span><ChevronRight size={15}/></button>
      <div className="nav-label">ПРОСТРАНСТВО</div>
      <nav aria-label="Основная навигация">
        <button className={view === 'board' ? 'active' : ''} onClick={() => navigate('board')}><LayoutDashboard size={19}/>Доска заявок<span className="count">{summary?.total ?? '—'}</span></button>
        <button className={view === 'list' ? 'active' : ''} onClick={() => navigate('list')}><ListTodo size={19}/>Все заявки</button>
        <button className={view === 'report' ? 'active' : ''} onClick={() => navigate('report')}><BarChart3 size={19}/>Обзор команды</button>
        <button className={view === 'team' ? 'active' : ''} onClick={() => navigate('team')}><Users size={19}/>Участники<span className="count plain">{team.length}</span></button>
      </nav>
      <div className="sidebar-bottom"><div className="demo-card"><span className="demo-badge">PET PROJECT / .NET</span><h3>Маленькие задачи.<br/>Большой порядок.</h3><p>Учебное приложение с настоящим API и базой данных.</p><a href="/openapi/v1.json" target="_blank" rel="noreferrer">Посмотреть API <ArrowUpRight size={15}/></a></div><div className="profile"><span className="avatar">IA</span><div>Idea to Action<small>Демонстрационная команда</small></div></div></div>
    </aside>
    {mobileNav && <button className="scrim" aria-label="Закрыть меню" onClick={() => setMobileNav(false)}/>}
    <div className="content"><header className="topbar"><div className="breadcrumbs"><button className="mobile-menu icon-button" aria-label="Открыть меню" onClick={() => setMobileNav(true)}><PanelLeftClose size={20}/></button><span>Рабочее пространство</span><ChevronRight size={14}/><strong>{view === 'report' ? 'Обзор команды' : view === 'team' ? 'Участники' : 'Заявки'}</strong></div><div className="top-right"><span className="live-dot"/>Локальное демо<span className="avatar tiny">IA</span></div></header>
    <main id="main"><div className="heading"><div><div className="eyebrow">МЕНЬШЕ ХАОСА. БОЛЬШЕ ФОКУСА.</div><h1>{view === 'report' ? 'Всё под контролем' : view === 'team' ? 'Люди за результатом' : 'Каждая задача — на своём месте'}</h1><p>{view === 'team' ? 'Вымышленные участники для демонстрации рабочих процессов.' : 'От первой идеи до готового результата. В одном пространстве.'}</p></div><button className="primary" onClick={() => setCreating(true)}><Plus size={18}/>Новая заявка</button></div>
      {error && <div role="alert" className="error"><AlertCircle size={18}/>{error}<button onClick={() => setRevision(x => x + 1)}>Повторить</button></div>}
      <div className="stats" aria-label="Общая статистика">{[
        ['Всего заявок', summary?.total, <Layers3 size={18}/>, 'Все задачи команды', 'all'],
        ['В работе', summary?.inProgress, <Clock3 size={18}/>, 'Двигаемся к результату', 'progress'],
        ['Завершено', summary?.done, <Check size={18}/>, 'Готово и можно выдохнуть', 'done'],
        ['Просрочено', summary?.overdue, <AlertCircle size={18}/>, 'Нужно немного внимания', 'overdue']
      ].map(([label, number, icon, hint, cls]) => <article className={'stat '+cls} key={label}><div className="stat-label">{label}<span>{icon}</span></div><strong>{number ?? '—'}</strong><small>{hint}</small></article>)}</div>
      {view === 'report' ? <Report summary={summary}/> : view === 'team' ? <div className="team-grid">{team.map(m => <article className="member" key={m.id}><span className={'avatar member-avatar m'+m.id}>{m.initials}</span><h2>{m.name}</h2><p>{m.role}</p><span className="member-badge">Участник демокоманды</span><button onClick={() => { setAssignee(String(m.id)); navigate('list'); }}>Посмотреть заявки <ArrowRight size={16}/></button></article>)}</div> : <>
      <div className="toolbar"><div className="view-tabs"><button className={view === 'board' ? 'chosen' : ''} onClick={() => setView('board')}><LayoutDashboard size={16}/>Доска</button><button className={view === 'list' ? 'chosen' : ''} onClick={() => setView('list')}><ListTodo size={16}/>Список</button></div><div className="toolbar-actions"><label className="search"><Search size={16}/><input aria-label="Поиск заявок" placeholder="Найти заявку..." value={search} onChange={e => setSearch(e.target.value)}/>{search && <button className="icon-button" aria-label="Очистить поиск" onClick={() => setSearch('')}><X size={14}/></button>}</label><button className={'secondary filter-button '+(filters ? 'selected' : '')} onClick={() => setFilters(x => !x)} aria-expanded={filters}><SlidersHorizontal size={15}/>Фильтры{hasFilters && <span className="filter-dot"/>}</button></div></div>
      {filters && <div className="filters"><label>Статус<select aria-label="Статус" value={status} onChange={e => setStatus(e.target.value)}><option value="">Все статусы</option>{Object.entries(statuses).map(([v,t]) => <option key={v} value={v}>{t}</option>)}</select></label><label>Приоритет<select aria-label="Приоритет" value={priority} onChange={e => setPriority(e.target.value)}><option value="">Все приоритеты</option>{Object.entries(priorities).map(([v,t]) => <option key={v} value={v}>{t}</option>)}</select></label><label>Исполнитель<select aria-label="Исполнитель" value={assignee} onChange={e => setAssignee(e.target.value)}><option value="">Все участники</option>{team.map(m => <option key={m.id} value={m.id}>{m.name}</option>)}</select></label><label>Порядок<select aria-label="Порядок" value={sort} onChange={e => setSort(e.target.value)}><option value="newest">Сначала новые</option><option value="priority">По приоритету</option><option value="due">По сроку</option></select></label><label className="checkbox"><input type="checkbox" checked={overdue} onChange={e => setOverdue(e.target.checked)}/>Просроченные</label>{hasFilters && <button className="text-button" onClick={clearFilters}>Сбросить</button>}</div>}
      <div className="board-caption"><span><span className="live-dot"/>{loading ? 'Загружаем заявки…' : `${data.total} заявок в выборке`}</span><span>Сроки считаются по UTC · {summary && date(summary.today)}</span></div>
      {loading ? <div className="loading" role="status">Загружаем рабочее пространство…</div> : !data.items.length ? <div className="empty"><Search size={28}/><h2>Здесь пока тихо</h2><p>{hasFilters ? 'Попробуйте изменить поиск или фильтры.' : 'Создайте первую заявку — и начнём.'}</p><button className="secondary" onClick={hasFilters ? clearFilters : () => setCreating(true)}>{hasFilters ? 'Сбросить фильтры' : 'Создать заявку'}</button></div> : view === 'board' ? <div className="board">{Object.entries(statuses).map(([key,label]) => <section className="column" key={key} aria-label={label}><div className="column-title"><span className={'status-dot '+key}/><h2>{label}</h2><span className="column-count">{data.items.filter(x => x.status === key).length}</span>{key === 'New' && <button className="icon-button" aria-label="Добавить заявку" onClick={() => setCreating(true)}><Plus size={17}/></button>}</div><div className="cards">{data.items.filter(x => x.status === key).map(item => <TaskCard key={item.id} item={item} onClick={() => setSelected(item.id)}/>)}{!data.items.some(x => x.status === key) && <p className="column-empty">Нет заявок на этой странице</p>}</div></section>)}</div> : <div className="table-wrap"><table><thead><tr><th>Заявка</th><th>Статус</th><th>Приоритет</th><th>Исполнитель</th><th>Срок</th></tr></thead><tbody>{data.items.map(item => <tr key={item.id}><td><button className="table-title" onClick={() => setSelected(item.id)}><small>TF-{String(item.id).padStart(3,'0')}</small>{item.title}</button></td><td><span className={'status-label '+item.status}>{statuses[item.status]}</span></td><td><PriorityBadge value={item.priority}/></td><td>{item.assigneeName || 'Не назначен'}</td><td className={item.overdue ? 'overdue-text' : ''}>{date(item.dueDate)}</td></tr>)}</tbody></table></div>}
      <div className="pagination"><span>Страница {page} из {Math.max(data.totalPages,1)} · до 12 заявок на странице</span><div><button className="icon-button" aria-label="Предыдущая страница" disabled={page <= 1 || loading} onClick={() => setPage(x => x - 1)}><ChevronLeft size={18}/></button><button className="icon-button" aria-label="Следующая страница" disabled={page >= data.totalPages || loading} onClick={() => setPage(x => x + 1)}><ChevronRight size={18}/></button></div></div></>}
      <footer><span><Layers3 size={14}/>TASKFLOW / IDEA TO ACTION</span><span>C# · ASP.NET Core · Entity Framework · React</span></footer>
    </main></div>
    {(creating || selected !== null) && <Editor id={selected} team={team} close={() => { setCreating(false); setSelected(null); }} saved={refresh}/>}
    {notice && <div role="status" className="toast"><Check size={17}/>{notice}</div>}
  </div>;
}

function PriorityBadge({value}) { return <span className={'priority '+value}><span/>{priorities[value]}</span>; }
function TaskCard({item,onClick}) {
  return <button className="task-card" onClick={onClick}><div className="card-top"><span className="task-id">TF-{String(item.id).padStart(3,'0')}</span><PriorityBadge value={item.priority}/></div><h3>{item.title}</h3><p>{item.description}</p><div className="card-bottom"><span className={item.overdue ? 'due overdue-text' : 'due'}><CalendarDays size={13}/>{date(item.dueDate)}</span>{item.assigneeName ? <span className={'avatar small m'+item.assigneeId} title={item.assigneeName}>{item.initials}</span> : <span className="unassigned">Не назначен</span>}</div></button>;
}
function Report({summary}) {
  if (!summary) return <p role="status">Загрузка отчёта…</p>;
  return <div className="report-grid"><section className="report-card"><div className="section-head"><div><h2>Движение заявок</h2><p>Состояние всей команды, без фильтров</p></div><BarChart3 size={22}/></div><div className="bar-chart">{[['Новые',summary.new,'New'],['В работе',summary.inProgress,'InProgress'],['Завершены',summary.done,'Done']].map(([label,count,cls]) => <div key={cls} className="bar-row"><span>{label}</span><div className="track"><div className={'bar '+cls} style={{width:`${summary.total ? count/summary.total*100 : 0}%`}}/></div><b>{count}</b></div>)}</div><div className="report-caption">{summary.total ? Math.round(summary.done/summary.total*100) : 0}% заявок завершено</div></section><section className="report-card attention"><span className="eyebrow">ТОЧКИ ВНИМАНИЯ</span><h2>Сфокусироваться<br/>на важном.</h2><div className="attention-row"><span>Просроченные заявки</span><b>{summary.overdue}</b></div><div className="attention-row"><span>Открытые с высоким приоритетом</span><b>{summary.highPriority}</b></div><p>Завершённые заявки не считаются просроченными.</p></section><section className="report-card export"><div><h2>Данные с собой</h2><p>CSV со всеми заявками, исполнителями и сроками. Открывается в Excel и Google Sheets.</p></div><a className="secondary" href="/api/reports/export" download><Download size={17}/>Скачать CSV</a></section></div>;
}

function Editor({id,team,close,saved}) {
  const ref = useRef(null), titleRef = useRef(null);
  const [item,setItem] = useState(null), [history,setHistory] = useState([]);
  const [form,setForm] = useState({title:'',description:'',priority:'Medium',assigneeId:'',dueDate:''});
  const [loading,setLoading] = useState(id !== null), [busy,setBusy] = useState(false), [error,setError] = useState('');
  useEffect(() => { const dialog=ref.current; dialog.showModal(); return () => dialog.close(); }, []);
  const apply = data => { setItem(data); setForm({ title:data.title, description:data.description, priority:data.priority, assigneeId:data.assigneeId ?? '', dueDate:data.dueDate ?? '' }); };
  useEffect(() => {
    if (id === null) { titleRef.current?.focus(); return; }
    const controller=new AbortController();
    Promise.all([api(`/items/${id}`,{signal:controller.signal}),api(`/items/${id}/history`,{signal:controller.signal})])
      .then(([data,events])=>{apply(data);setHistory(events);}).catch(e=>{if(e.name!=='AbortError')setError(e.message);})
      .finally(()=>{if(!controller.signal.aborted)setLoading(false);});
    return ()=>controller.abort();
  },[id]);
  const field = name => ({value:form[name],onChange:e=>setForm({...form,[name]:e.target.value})});
  async function submit(e) {
    e.preventDefault();setBusy(true);setError('');
    const input={...form,assigneeId:form.assigneeId ? Number(form.assigneeId) : null,dueDate:form.dueDate || null};
    try { await api(id===null?'/items':`/items/${id}`,{method:id===null?'POST':'PUT',body:JSON.stringify({...input,...(item?{version:item.version}:{})})});saved(id===null?'Заявка создана':'Изменения сохранены');close(); }
    catch(e){setError(e.message);}finally{setBusy(false);}
  }
  async function changeStatus(status) {
    setBusy(true);setError('');
    try { const next=await api(`/items/${id}/status`,{method:'PATCH',body:JSON.stringify({status,version:item.version})});apply(next);setHistory(await api(`/items/${id}/history`));saved('Статус обновлён'); }
    catch(e){setError(e.message);}finally{setBusy(false);}
  }
  async function reload() {setLoading(true);setError('');try {apply(await api(`/items/${id}`));setHistory(await api(`/items/${id}/history`));}catch(e){setError(e.message);}finally{setLoading(false);}}
  return <dialog ref={ref} className="editor" onCancel={e=>{if(busy)e.preventDefault();else close();}} aria-labelledby="editor-title"><div className="editor-head"><span className="eyebrow">{id===null?'НОВАЯ ЗАЯВКА':`TF-${String(id).padStart(3,'0')}`}</span><button className="icon-button" aria-label="Закрыть карточку" disabled={busy} onClick={close}><X size={21}/></button></div><h2 id="editor-title">{id===null?'Начнём с задачи':'Детали заявки'}</h2><p className="editor-sub">Понятная задача — первый шаг к результату.</p>
    {error && <div role="alert" className="error">{error}{id!==null&&<button onClick={reload}>Обновить карточку</button>}</div>}
    {loading ? <p role="status">Загрузка карточки…</p> : id!==null&&!item ? <p>Заявка недоступна.</p> : <><form onSubmit={submit}><label>Название<input ref={titleRef} required minLength={3} maxLength={120} {...field('title')} placeholder="Что нужно сделать?"/></label><label>Описание<textarea maxLength={4000} {...field('description')} placeholder="Контекст, ожидаемый результат, важные детали"/></label><div className="form-grid"><label>Исполнитель<select aria-label="Исполнитель" {...field('assigneeId')}><option value="">Не назначен</option>{team.map(m=><option key={m.id} value={m.id}>{m.name}</option>)}</select></label><label>Приоритет<select aria-label="Приоритет" {...field('priority')}>{Object.entries(priorities).map(([k,v])=><option key={k} value={k}>{v}</option>)}</select></label></div><label>Срок выполнения<input type="date" {...field('dueDate')}/></label><button className="primary" disabled={busy}>{busy?'Сохраняем…':id===null?'Создать заявку':'Сохранить изменения'}<ArrowRight size={16}/></button></form>
    {item && <><section className="status-actions"><h3>Статус: {statuses[item.status]}</h3><p>Сначала сохраните изменения полей, затем меняйте статус.</p><div>{item.status==='New'&&<button className="secondary" disabled={busy} onClick={()=>changeStatus('InProgress')}>Взять в работу</button>}{item.status==='InProgress'&&<><button className="primary" disabled={busy} onClick={()=>changeStatus('Done')}><Check size={16}/>Завершить</button><button className="secondary" disabled={busy} onClick={()=>changeStatus('New')}>Вернуть в новые</button></>}{item.status==='Done'&&<button className="secondary" disabled={busy} onClick={()=>changeStatus('InProgress')}>Вернуть в работу</button>}</div></section><section className="history"><h3>История изменений</h3>{history.map(event=><div className="history-event" key={event.id}><Circle size={8}/><div>{event.action}<small>{new Date(event.at.endsWith('Z')?event.at:event.at+'Z').toLocaleString('ru-RU')}</small></div></div>)}</section></>}
    </>}
  </dialog>;
}
createRoot(document.getElementById('root')).render(<App/>);
