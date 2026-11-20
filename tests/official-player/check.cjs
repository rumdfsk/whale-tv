const {createRequire} = require('node:module');
const {readFileSync} = require('node:fs');
const {resolve} = require('node:path');
const assert = require('node:assert/strict');
const {JSDOM} = createRequire(resolve(process.argv[2] || __dirname, 'package.json'))('jsdom');
const script = readFileSync(resolve(__dirname, '../../src/TVLive/Web/official-player.js'),'utf8').replace('__TVLIVE_CHANNEL__','null');
const markup = '<header>官网导航</header><main><aside>频道列表</aside><section><div class="c-container"><div class="video-con"><video></video></div><div class="poster"><div class="voice off"></div><div class="login">登录与版权提示</div></div></div></section></main><footer>页脚</footer>';
function setup(html=markup,url='https://www.yangshipin.cn/tv/home?pid=600001859') {
 const dom=new JSDOM(html,{url,runScripts:'outside-only'}); const w=dom.window;
 let tick; w.setInterval=fn=>{tick=fn;return 1}; w.clearInterval=()=>{tick=null};
 function configure(v) {if(!v)return;Object.defineProperties(v,{paused:{value:true,writable:true},readyState:{value:4,writable:true},videoWidth:{value:1920},videoHeight:{value:1080}});v.muted=true;v.play=async()=>{v.paused=false};v.pause=()=>{v.paused=true};}
 configure(w.document.querySelector('video')); return {dom,w,configure,tick:()=>tick?.()};
}
(async()=>{
 const {dom,w,tick}=setup();const v=w.document.querySelector('video'),root=v.closest('.c-container');
 assert.equal(w.eval(script).found,true);assert.equal(v.closest('.c-container'),root);
 assert.equal(root.hasAttribute('data-tvlive-player'),true);assert.equal(w.document.querySelector('.login').textContent,'登录与版权提示');
 assert.equal(w.document.querySelector('aside').hasAttribute('data-tvlive-path'),false);
 console.log('PASS player kept in original DOM; navigation excluded; notices preserved');
 await w.__tvLive.command('start',.6);assert.equal(v.paused,false);assert.equal(v.muted,false);assert.equal(v.volume,.6);
 await w.__tvLive.command('volume',0);assert.equal(v.muted,true);assert.equal(v.volume,0);
 console.log('PASS start, unmute and intentional zero volume');
 await w.__tvLive.command('toggle');tick();assert.equal(v.paused,true);console.log('PASS paused playback is not restarted by layout refresh');
 let message;w.chrome={webview:{postMessage:m=>message=m}};w.document.dispatchEvent(new w.KeyboardEvent('keydown',{key:'PageDown',bubbles:true,cancelable:true}));assert.equal(message.key,'PageDown');
 for(const key of ['ArrowLeft','ArrowRight','ArrowUp','ArrowDown',' ']) {
 message=null;w.document.dispatchEvent(new w.KeyboardEvent('keydown',{key,bubbles:true,cancelable:true}));assert.equal(message.key,key);
 }
 message=null;w.document.dispatchEvent(new w.KeyboardEvent('keydown',{key:' ',repeat:true,bubbles:true,cancelable:true}));assert.equal(message,null);
 const input=w.document.createElement('input');root.appendChild(input);input.dispatchEvent(new w.KeyboardEvent('keydown',{key:'ArrowLeft',bubbles:true,cancelable:true}));assert.equal(message,null);
 console.log('PASS arrow/space keys forwarded; space repeats and text editing protected');
 v.play=async()=>{const e=new Error();e.name='NotAllowedError';throw e};assert.equal((await w.__tvLive.command('start',.6)).needsClick,true);
 console.log('PASS blocked autoplay reports manual interaction');
 await w.__tvLive.command('restore');assert.equal(w.document.querySelectorAll('[data-tvlive-path],[data-tvlive-player],#tvlive-player-style').length,0);assert.equal(w.__tvLive,undefined);
 console.log('PASS full-page restoration removes styles, markers and adapter');dom.window.close();
 const delayed=setup('<header>导航</header><main></main>');assert.equal(delayed.w.eval(script).found,false);assert.equal(delayed.w.document.querySelector('#tvlive-player-style'),null);
 delayed.w.document.querySelector('main').innerHTML=markup;delayed.configure(delayed.w.document.querySelector('video'));delayed.tick();assert.equal(delayed.w.__tvLive.state().found,true);console.log('PASS delayed video; no hiding before video exists');delayed.dom.window.close();
 for (const [url,rootHtml] of [
 ['https://www.cztv.com/liveTV/102','<div class="videoKS"><div data-kp-role="videoArea"><video></video></div><div data-kp-role="mute-btn"></div></div>'],
 ['https://www.gdtv.cn/tvChannelDetail/44','<div id="J_prismPlayer"><video></video><div class="prism-big-play-btn"></div><div class="rights">版权提示</div></div>'],
 ['https://news.hbtv.com.cn/app/tv/432','<div id="player-con"><video></video><div class="prism-big-play-btn"></div></div>'],
 ['https://tv.hoolo.tv/hzzh/','<div id="m2o_liveplayer"><video></video><div class="prism-big-play-btn"></div></div>']
 ]) {
 const local=setup('<header>导航</header><main>'+rootHtml+'</main>',url);assert.equal(local.w.eval(script).found,true);
 let clicks=0;local.w.document.querySelector('.prism-big-play-btn')?.addEventListener('click',()=>clicks++);
 await local.w.__tvLive.command('start',.55);assert.equal(local.w.document.querySelector('video').paused,false);
 if(local.w.document.querySelector('.prism-big-play-btn'))assert.equal(clicks,1);
 await local.w.__tvLive.command('restore');assert.equal(local.w.document.querySelector('[data-tvlive-player]'),null);local.dom.window.close();
 }
 console.log('PASS Zhejiang, Guangdong, Hubei and Hangzhou player/play-button/restore adapters');
 const sh=setup('<ul><li class="active"><img alt="东方卫视"></li><li><img alt="新闻综合"></li></ul><div class="live-box"><div class="live-player"><video></video></div><div class="image-mask">官网版权提示</div></div>','https://live.kankanews.com/huikan/');
 let now=1000;sh.w.Date.now=()=>now;
 const target=sh.w.document.querySelector('img[alt="新闻综合"]').parentElement;
 target.addEventListener('click',()=>{sh.w.document.querySelector('li.active').classList.remove('active');target.classList.add('active')});
 const selectedScript=readFileSync(resolve(__dirname,'../../src/TVLive/Web/official-player.js'),'utf8').replace('__TVLIVE_CHANNEL__',JSON.stringify('新闻综合'));
 assert.equal(sh.w.eval(selectedScript).found,false);assert.equal(sh.w.document.querySelector('[data-tvlive-player]'),null);
 now+=1500;sh.tick();assert.equal(sh.w.__tvLive.state().found,true);assert.equal(target.classList.contains('active'),true);
 sh.w.document.querySelector('video').remove();sh.tick();assert.equal(sh.w.__tvLive.state().found,false);assert.equal(sh.w.__tvLive.state().focused,true);assert.equal(sh.w.document.querySelector('.image-mask').textContent,'官网版权提示');
 await sh.w.__tvLive.command('restore');sh.dom.window.close();
 console.log('PASS Shanghai selects requested channel before focus; no-video rights notice retained');
 const foreign=setup(markup,'https://example.com/');assert.equal(foreign.w.eval(script).supported,false);assert.equal(foreign.w.document.querySelector('#tvlive-player-style'),null);console.log('PASS unrelated sites untouched');foreign.dom.window.close();
})().catch(e=>{console.error(e);process.exitCode=1});
