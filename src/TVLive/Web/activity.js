(() => {
  // Independent of video adaptation so full-page mode and loading pages also wake the app controls.
  if (window.__tvLiveActivityInstalled) return;
  window.__tvLiveActivityInstalled = true;
  let lastMove = -Infinity;
  function activity(event) {
    if (!event.isTrusted || !window.chrome?.webview) return;
    const now = performance.now();
    if (event.type === 'pointermove' && now - lastMove < 100) return;
    if (event.type === 'pointermove') lastMove = now;
    window.chrome.webview.postMessage({type:'tvlive-activity'});
  }
  for (const name of ['pointermove','pointerdown','pointerup','keydown','keyup','wheel'])
    document.addEventListener(name, activity, {capture:true, passive:true});
})()
