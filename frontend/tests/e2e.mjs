import { chromium, expect } from '@playwright/test';
import { spawn } from 'node:child_process';
import { mkdir, writeFile, unlink } from 'node:fs/promises';
import { existsSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const artifacts = path.join(root, 'artifacts');
const shots = path.join(root, 'docs/screenshots');
await mkdir(artifacts, { recursive: true }); await mkdir(shots, { recursive: true });
const port = 5081, base = process.env.TASKFLOW_BASE_URL || `http://127.0.0.1:${port}`;
const db = path.join(artifacts, `browser-test-${Date.now()}.db`);
let server, browser;
const localDotnet = path.join(root, '.tools/dotnet', process.platform === 'win32' ? 'dotnet.exe' : 'dotnet');
const dotnet = existsSync(localDotnet) ? localDotnet : 'dotnet';
const report = { tests: [], errors: [] };
const pass = name => { report.tests.push(name); console.log('PASS:', name); };
try {
  if (!process.env.TASKFLOW_BASE_URL) {
    server = spawn(dotnet, ['run','--project',path.join(root,'src/TaskFlow.Api'),'--no-build','--urls',base], {
      cwd:root, windowsHide:true, stdio:'ignore', env:{...process.env, ConnectionStrings__Sqlite:`Data Source=${db}`, Demo__Seed:'true', Logging__LogLevel__Default:'Warning'}
    });
    let ready=false;
    for(let i=0;i<80;i++) { if(server.exitCode!==null)throw Error('Test API process exited');try { if((await fetch(base+'/api/health')).ok){ready=true;break;} }catch{} await new Promise(r=>setTimeout(r,250)); }
    if(!ready)throw Error('Test API did not become ready');
  }
  browser = await chromium.launch({headless:true, channel:process.platform==='win32'?'msedge':undefined});
  const context=await browser.newContext({viewport:{width:1440,height:1050},reducedMotion:'reduce'});
  const page=await context.newPage();page.on('pageerror', e=>report.errors.push(e.message));
  await page.goto(base);await expect(page.locator('.task-card')).toHaveCount(9);
  await expect(page.getByRole('heading',{level:1})).toContainText('Каждая задача');
  await page.screenshot({path:path.join(shots,'board-desktop.png'),fullPage:true});pass('Real API-backed board shows nine seeded requests');
  await page.getByRole('button',{name:'Обзор команды',exact:true}).click();await expect(page.locator('.report-grid')).toBeVisible();
  const downloadPromise=page.waitForEvent('download');await page.getByRole('link',{name:'Скачать CSV'}).click();const download=await downloadPromise;await download.saveAs(path.join(artifacts,'browser-export.csv'));
  await page.screenshot({path:path.join(shots,'report-desktop.png'),fullPage:true});pass('Summary and CSV attachment');
  await page.getByRole('button',{name:/Участники/}).click();await expect(page.locator('.member')).toHaveCount(3);await page.screenshot({path:path.join(shots,'team-desktop.png'),fullPage:true});
  await page.getByRole('button',{name:'Новая заявка',exact:true}).click();
  await page.getByLabel('Название',{exact:true}).fill('E2E проверка рабочего процесса');
  await page.getByLabel('Описание',{exact:true}).fill('Create, assign, start and complete a real database-backed request.');
  await page.getByLabel('Срок выполнения',{exact:true}).fill('2026-11-20');
  await page.getByRole('button',{name:'Создать заявку',exact:true}).click();await expect(page.locator('dialog')).toHaveCount(0);
  await page.getByRole('button',{name:/Доска заявок/}).click();await page.getByLabel('Поиск заявок').fill('E2E');await expect(page.locator('.task-card')).toHaveCount(1);
  await page.locator('.task-card').click();await page.getByRole('button',{name:'Взять в работу',exact:true}).click();await expect(page.getByRole('alert')).toContainText('назначьте исполнителя');pass('Unassigned work cannot start (real server-side rule)');
  await page.getByLabel('Исполнитель',{exact:true}).selectOption({label:'Анна Мороз'});await page.getByRole('button',{name:'Сохранить изменения',exact:true}).click();
  await page.locator('.task-card').click();await page.getByRole('button',{name:'Взять в работу',exact:true}).click();await expect(page.locator('.status-actions h3')).toContainText('В работе');
  await page.getByRole('button',{name:'Завершить',exact:true}).click();await expect(page.locator('.status-actions h3')).toContainText('Завершена');await expect(page.locator('.history-event')).toHaveCount(4);
  await page.screenshot({path:path.join(shots,'request-history.png'),fullPage:true});await page.getByRole('button',{name:'Закрыть карточку',exact:true}).click();pass('Create → assign → start → complete persists with four history entries');
  await page.reload();await expect(page.locator('.task-card').filter({hasText:'E2E проверка'})).toHaveCount(1);pass('Request survives browser reload');
  await page.getByRole('button',{name:'Фильтры',exact:true}).click();await page.getByLabel('Приоритет',{exact:true}).selectOption('High');await expect(page.locator('.task-card')).toHaveCount(3); // Three seeded High requests, one completed.
  await page.getByRole('button',{name:'Сбросить',exact:true}).click();
  await page.getByLabel('Поиск заявок').fill('no-such-request-183927');await expect(page.getByRole('heading',{name:'Здесь пока тихо'})).toBeVisible();await page.getByRole('button',{name:'Сбросить фильтры',exact:true}).click();await expect(page.locator('.task-card')).toHaveCount(10);pass('Filters and empty-state reset');
  await page.getByRole('button',{name:'Список',exact:true}).click();await expect(page.locator('tbody tr')).toHaveCount(10);
  await page.screenshot({path:path.join(shots,'list-desktop.png'),fullPage:true});pass('List view');
  const response=await page.request.get(base+'/api/items?pageSize=100');const items=await response.json();const task=items.items.find(x=>x.title.startsWith('E2E'));
  const update={title:task.title+' edited',description:task.description,priority:task.priority,assigneeId:task.assigneeId,dueDate:task.dueDate,version:task.version};
  expect((await page.request.put(base+`/api/items/${task.id}`,{data:update})).status()).toBe(200);
  expect((await page.request.put(base+`/api/items/${task.id}`,{data:update})).status()).toBe(409);pass('Stale version conflict');
  await page.setViewportSize({width:390,height:844});await page.getByRole('button',{name:'Открыть меню',exact:true}).click();await page.getByRole('button',{name:/Доска заявок/}).click();
  await expect(page.locator('.sidebar')).not.toBeVisible();await expect(page.locator('.task-card')).toHaveCount(10);
  expect(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth)).toBe(true);
  await page.screenshot({path:path.join(shots,'board-mobile.png'),fullPage:true});pass('390px mobile board / menu / no horizontal overflow');
  await page.getByRole('button',{name:'Новая заявка',exact:true}).click();await expect(page.getByLabel('Название',{exact:true})).toBeVisible();await page.keyboard.press('Escape');await expect(page.locator('dialog')).toHaveCount(0);pass('Keyboard dialog dismissal');
  await page.route('**/api/items?**',r=>r.abort());await page.reload();await expect(page.getByRole('alert')).toBeVisible();await page.unroute('**/api/items?**');await page.getByRole('button',{name:'Повторить',exact:true}).click();await expect(page.locator('.task-card')).toHaveCount(10);pass('Network failure and retry');
  expect(report.errors).toEqual([]);pass('No browser JavaScript errors');
  report.result='passed';
} catch(error) { report.result='failed';report.failure=String(error);throw error; }
finally {await browser?.close();if(server){server.kill();await new Promise(r=>{if(server.exitCode!==null)return r();server.once('exit',r);setTimeout(r,2000);});for(const suffix of ['', '-wal','-shm'])await unlink(db+suffix).catch(()=>{});}await writeFile(path.join(artifacts,'browser-verification.json'),JSON.stringify(report,null,2));}
