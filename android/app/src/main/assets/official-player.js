((requestedChannel) => {
  // Whitelisted official providers, inspected from tvyan's directory and their live pages.
  const configs = [
    {host:'www.yangshipin.cn', path:/^\/tv\/home$/, root:'.c-container', video:'.c-container video', mute:'.voice.off'},
    {host:'www.cztv.com', path:/^\/liveTV(?:\/\d+)?\/?$/, root:'.videoKS', video:'.videoKS video', mute:'[data-kp-role="mute-btn"]'},
    {host:'live.kankanews.com', path:/^\/huikan\/?$/, root:'.live-box', video:'.live-box video', select:true},
    {host:'www.gdtv.cn', path:/^\/tvChannelDetail\/\d+\/?$/, root:'#J_prismPlayer', video:'#J_prismPlayer video', play:'.prism-big-play-btn'},
    {host:'news.hbtv.com.cn', path:/^\/app\/tv\/\d+\/?$/, root:'#player-con', video:'#player-con video', play:'.prism-big-play-btn'},
    {host:'tv.hoolo.tv', path:/^\/(hzzh|xhmz|hzsh|utv|hzse)\/$/, root:'#m2o_liveplayer', video:'#m2o_liveplayer video', play:'.prism-big-play-btn'}
  ];
  const config = configs.find(c => location.hostname === c.host && c.path.test(location.pathname));
  if (!config) return {supported:false};
  if (window.__tvLive) return window.__tvLive.state();
  let selected = !config.select || !requestedChannel, selectionTime = 0;
  function selectChannel() {
    if (selected) return true;
    const image = Array.from(document.querySelectorAll('li img[alt]')).find(e => e.getAttribute('alt') === requestedChannel);
    const item = image?.closest('li');
    if (!item) return false;
    if (!item.classList.contains('active')) {
      if (!selectionTime) { selectionTime = Date.now(); item.click(); }
      return false;
    }
    if (selectionTime && Date.now() - selectionTime < 1000) return false;
    selected = true; return true;
  }
  let root = null, video = null, timer = null, desiredVolume = .6, ready = false;
  const style = document.createElement('style');
  style.id = 'tvlive-player-style';
  style.textContent = `
    html[data-tvlive-path], body[data-tvlive-path] { margin:0!important; padding:0!important; overflow:hidden!important; background:#000!important; }
    [data-tvlive-path] { transform:none!important; filter:none!important; perspective:none!important; contain:none!important; overflow:visible!important; }
    [data-tvlive-path] > :not([data-tvlive-path]):not([data-tvlive-player]):not(style):not(script) { display:none!important; }
    [data-tvlive-player] { position:fixed!important; inset:0!important; width:100vw!important; height:100vh!important; max-width:none!important; max-height:none!important; margin:0!important; padding:0!important; z-index:2147483000!important; background:#000!important; }
    [data-tvlive-player] .video-con { position:absolute!important; inset:0!important; width:100%!important; height:100%!important; }
    [data-tvlive-player].videoKS > div, [data-tvlive-player] [data-kp-role="videoArea"], [data-tvlive-player] [class^="_kwai-player-video_"] { width:100%!important; height:100%!important; }
    [data-tvlive-player] .live-player { width:100%!important; height:100%!important; padding-top:0!important; }
    [data-tvlive-player] .prism-fullscreen-btn, [data-tvlive-player] [data-kp-role="fullscreen-btn"], [data-tvlive-player] xg-fullscreen { display:none!important; }
    [data-tvlive-player] video { width:100%!important; height:100%!important; object-fit:contain!important; }
    [data-tvlive-player] .videoFull, [data-tvlive-player] .full, [data-tvlive-player] .pip { display:none!important; }
  `;
  function clearMarks() {
    document.querySelectorAll('[data-tvlive-path],[data-tvlive-player]').forEach(e => {
      e.removeAttribute('data-tvlive-path'); e.removeAttribute('data-tvlive-player');
    });
  }
  function refresh() {
    if (!selectChannel()) return false;
    const candidates = Array.from(document.querySelectorAll(config.video));
    const next = candidates.sort((a,b) => (b.videoWidth*b.videoHeight || b.clientWidth*b.clientHeight) - (a.videoWidth*a.videoHeight || a.clientWidth*a.clientHeight))[0] || null;
    const nextRoot = next?.closest(config.root) || (config.select ? document.querySelector(config.root) : null);
    if (!nextRoot) return false;
    if (next !== video || nextRoot !== root || !root?.isConnected) {
      clearMarks(); video = next; root = nextRoot;
      root.setAttribute('data-tvlive-player','');
      for (let p = root.parentElement; p; p = p.parentElement) p.setAttribute('data-tvlive-path','');
      if (!style.isConnected) document.head.appendChild(style);
      ready = true;
    }
    return !!video;
  }
  function keydown(event) {
    if (event.target?.closest?.("input,textarea,[contenteditable=true]")) return;
    if (!["F11","Escape","PageUp","PageDown","ArrowUp","ArrowDown","ArrowLeft","ArrowRight","c","C"," "].includes(event.key)) return;
    if (!window.chrome?.webview) return;
    event.preventDefault(); event.stopImmediatePropagation();
    if (event.repeat && event.key === " ") return;
    window.chrome.webview.postMessage({type:"tvlive-key", key:event.key});
  }
  function state() {
    return {supported:true, selected, found:!!video?.isConnected, focused:ready && style.isConnected,
      playing:!!video && !video.paused && video.readyState >= 2,
      muted:video ? video.muted : true, volume:video ? video.volume : 0,
      width:video ? video.videoWidth : 0, height:video ? video.videoHeight : 0};
  }
  async function command(action, volume) {
    if (action === 'restore') { clearInterval(timer); document.removeEventListener('keydown', keydown, true); clearMarks(); style.remove(); delete window.__tvLive; return {restored:true}; }
    if (!refresh()) return state();
    if (Number.isFinite(volume)) desiredVolume = Math.max(0,Math.min(1,volume));
    if (action === 'start' || action === 'volume') {
      // Use the site's actual mute control to keep its UI in sync, then apply application volume.
      const unmute = config.mute ? root.querySelector(config.mute) : null;
      if (desiredVolume > 0 && video.muted && unmute) unmute.click();
      video.muted = desiredVolume === 0; video.volume = desiredVolume;
    }
    try {
      if (action === 'start' || (action === 'toggle' && video.paused)) {
        const startButton = config.play ? root.querySelector(config.play) : null;
        if (video.paused && startButton && getComputedStyle(startButton).display !== 'none') startButton.click();
        await video.play();
      }
      else if (action === 'toggle') video.pause();
      return state();
    } catch (error) { return {...state(), needsClick:true, error:error.name}; }
  }
  window.__tvLive = {state, command};
  document.addEventListener('keydown', keydown, true);
  refresh(); timer = setInterval(refresh, 1000);
  return state();
})(__TVLIVE_CHANNEL__)
