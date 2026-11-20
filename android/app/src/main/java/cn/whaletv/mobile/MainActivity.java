package cn.whaletv.mobile;

import android.app.Activity;
import android.app.AlertDialog;
import android.content.*;
import android.content.pm.ActivityInfo;
import android.content.res.Configuration;
import android.graphics.Color;
import android.media.AudioManager;
import android.net.Uri;
import android.os.*;
import android.view.*;
import android.webkit.*;
import android.widget.*;
import androidx.media3.common.*;
import androidx.media3.datasource.DefaultHttpDataSource;
import androidx.media3.exoplayer.ExoPlayer;
import androidx.media3.exoplayer.hls.HlsMediaSource;
import androidx.media3.ui.PlayerView;
import java.nio.charset.StandardCharsets;
import java.util.Map;
import java.util.concurrent.atomic.AtomicBoolean;

@androidx.media3.common.util.UnstableApi
public final class MainActivity extends Activity {
    private final Handler handler=new Handler(Looper.getMainLooper());
    private LinearLayout root, header, controls, channelList;
    private FrameLayout stage;
    private ScrollView scroll;
    private TextView status, title;
    private PlayerView playerView;
    private ExoPlayer player;
    private WebView official, resolver;
    private int current, generation;
    private boolean webMode, full, foreground=true, fullPage, needsReconnect, adapted;
    private String adapter, userAgent;
    private final Runnable hide=()->{ if(controls!=null) controls.setVisibility(View.GONE); };
    private AudioManager audio;

    @Override public void onCreate(Bundle saved) {
        super.onCreate(saved);
        getWindow().addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON);
        setVolumeControlStream(AudioManager.STREAM_MUSIC);
        audio=(AudioManager)getSystemService(AUDIO_SERVICE);
        try { adapter=new String(readAsset("official-player.js"),StandardCharsets.UTF_8).replace("__TVLIVE_CHANNEL__","null"); }
        catch(Exception e) { adapter="({supported:false})"; }
        root=new LinearLayout(this); root.setOrientation(LinearLayout.VERTICAL); root.setBackgroundColor(Color.rgb(12,22,32));
        setContentView(root);
        root.setOnApplyWindowInsetsListener((v,insets)->{ if(Build.VERSION.SDK_INT>=30){android.graphics.Insets bars=insets.getInsets(WindowInsets.Type.systemBars()); root.setPadding(bars.left,bars.top,bars.right,bars.bottom);}else{root.setPadding(insets.getSystemWindowInsetLeft(),insets.getSystemWindowInsetTop(),insets.getSystemWindowInsetRight(),insets.getSystemWindowInsetBottom());} return insets; });
        header=new LinearLayout(this); header.setPadding(dp(16),dp(12),dp(16),dp(12)); header.setGravity(Gravity.CENTER_VERTICAL);
        ImageView icon=new ImageView(this); icon.setImageResource(R.drawable.whale_tv); header.addView(icon,new LinearLayout.LayoutParams(dp(44),dp(44)));
        title=text("鲸鱼电视 · 手机体验版",19); header.addView(title,new LinearLayout.LayoutParams(0,-2,1)); root.addView(header);
        stage=new FrameLayout(this); stage.setBackgroundColor(Color.BLACK); root.addView(stage);
        playerView=(PlayerView)getLayoutInflater().inflate(R.layout.player,stage,false); stage.addView(playerView);
        player=new ExoPlayer.Builder(this).build(); player.setAudioAttributes(new androidx.media3.common.AudioAttributes.Builder().setUsage(C.USAGE_MEDIA).setContentType(C.AUDIO_CONTENT_TYPE_MOVIE).build(),true); playerView.setPlayer(player);
        player.addListener(new Player.Listener(){
            @Override public void onPlaybackStateChanged(int state){ if(!webMode && state==Player.STATE_READY) setStatus("正在播放 · "+Channel.ALL[current].name); }
            @Override public void onPlayerError(PlaybackException e){ if(!webMode) setStatus("播放未成功，可点「更多」重试或打开官网模式"); }
        });
        official=webView(false); official.setVisibility(View.GONE); stage.addView(official,new FrameLayout.LayoutParams(-1,-1));
        controls=new LinearLayout(this); controls.setOrientation(LinearLayout.VERTICAL); controls.setPadding(dp(8),dp(4),dp(8),dp(4)); controls.setBackgroundColor(0xD0202E3B);
        LinearLayout row=new LinearLayout(this); controls.addView(row);
        button(row,"上一个",()->select(current-1)); button(row,"播放/暂停",this::toggle); button(row,"下一个",()->select(current+1)); button(row,"全屏",()->setFull(!full)); button(row,"更多",this::menu);
        LinearLayout volume=new LinearLayout(this); volume.setGravity(Gravity.CENTER_VERTICAL); volume.addView(text("音量",12));
        SeekBar seek=new SeekBar(this); seek.setMax(audio.getStreamMaxVolume(AudioManager.STREAM_MUSIC)); seek.setProgress(audio.getStreamVolume(AudioManager.STREAM_MUSIC)); volume.addView(seek,new LinearLayout.LayoutParams(0,dp(32),1)); controls.addView(volume);
        seek.setOnSeekBarChangeListener(new SeekBar.OnSeekBarChangeListener(){ public void onProgressChanged(SeekBar s,int n,boolean user){if(user){audio.setStreamVolume(AudioManager.STREAM_MUSIC,n,0);wake();}} public void onStartTrackingTouch(SeekBar s){wake();} public void onStopTrackingTouch(SeekBar s){wake();} });
        FrameLayout.LayoutParams cp=new FrameLayout.LayoutParams(-1,-2,Gravity.BOTTOM); stage.addView(controls,cp);
        scroll=new ScrollView(this); channelList=new LinearLayout(this); channelList.setOrientation(LinearLayout.VERTICAL); channelList.setPadding(dp(16),dp(12),dp(16),dp(20)); scroll.addView(channelList); root.addView(scroll,new LinearLayout.LayoutParams(-1,0,1));
        status=text("准备播放",14); channelList.addView(status); TextView tip=text("选择频道 · 双击画面切换全屏\n官网限制、登录和版权提示以页面为准",12); tip.setPadding(0,dp(12),0,dp(12)); channelList.addView(tip);
        for(int i=0;i<Channel.ALL.length;i++){final int n=i; Button b=new Button(this);b.setAllCaps(false);b.setText(Channel.ALL[i].name);b.setTextColor(Color.WHITE);b.setOnClickListener(v->select(n));channelList.addView(b,new LinearLayout.LayoutParams(-1,dp(52)));}
        playerView.setOnTouchListener(new View.OnTouchListener(){long last; public boolean onTouch(View v,android.view.MotionEvent e){if(e.getAction()==MotionEvent.ACTION_UP){long now=SystemClock.uptimeMillis();if(now-last<300)setFull(!full);last=now;v.performClick();}return true;}});
        layout(); current=Channel.wrap(getPreferences(0).getInt("channel",0)); select(current);
    }
    private byte[] readAsset(String name) throws java.io.IOException {try(java.io.InputStream input=getAssets().open(name);java.io.ByteArrayOutputStream out=new java.io.ByteArrayOutputStream()){byte[] buffer=new byte[8192];int count;while((count=input.read(buffer))!=-1)out.write(buffer,0,count);return out.toByteArray();}}
    private int dp(int n){return (int)(n*getResources().getDisplayMetrics().density+.5f);}
    private TextView text(String s,int size){TextView v=new TextView(this);v.setText(s);v.setTextSize(size);v.setTextColor(Color.rgb(225,239,248));return v;}
    private void button(LinearLayout row,String name,Runnable action){Button b=new Button(this);b.setText(name);b.setTextSize(11);b.setPadding(0,0,0,0);b.setAllCaps(false);row.addView(b,new LinearLayout.LayoutParams(0,dp(44),1));b.setOnClickListener(v->{wake();action.run();});}
    private void setStatus(String value){status.setText(value);}
    private void wake(){if(controls==null)return;controls.setVisibility(View.VISIBLE);handler.removeCallbacks(hide);handler.postDelayed(hide,4000);}
    @Override public boolean dispatchTouchEvent(MotionEvent event){wake();return super.dispatchTouchEvent(event);}
    @Override public boolean dispatchKeyEvent(KeyEvent event){wake();return super.dispatchKeyEvent(event);}
    private void layout(){boolean landscape=getResources().getConfiguration().orientation==Configuration.ORIENTATION_LANDSCAPE; boolean expanded=full||landscape;header.setVisibility(expanded?View.GONE:View.VISIBLE);scroll.setVisibility(expanded?View.GONE:View.VISIBLE);stage.setLayoutParams(new LinearLayout.LayoutParams(-1,expanded?0: getResources().getDisplayMetrics().widthPixels*9/16,expanded?1:0));if(Build.VERSION.SDK_INT>=30){WindowInsetsController c=getWindow().getInsetsController();if(c!=null){c.setSystemBarsBehavior(WindowInsetsController.BEHAVIOR_SHOW_TRANSIENT_BARS_BY_SWIPE);if(expanded)c.hide(WindowInsets.Type.systemBars());else c.show(WindowInsets.Type.systemBars());}}}
    private void setFull(boolean value){full=value;setRequestedOrientation(value?ActivityInfo.SCREEN_ORIENTATION_SENSOR_LANDSCAPE:ActivityInfo.SCREEN_ORIENTATION_UNSPECIFIED);layout();wake();}
    @Override public void onConfigurationChanged(Configuration config){super.onConfigurationChanged(config);layout();}
    @Override public void onBackPressed(){if(full||getResources().getConfiguration().orientation==Configuration.ORIENTATION_LANDSCAPE){full=false;setRequestedOrientation(ActivityInfo.SCREEN_ORIENTATION_PORTRAIT);layout();}else super.onBackPressed();}

    @android.annotation.SuppressLint("SetJavaScriptEnabled") private WebView webView(boolean hidden){
        WebView w=new WebView(this); WebSettings s=w.getSettings();s.setJavaScriptEnabled(true);s.setDomStorageEnabled(true);s.setAllowFileAccess(false);s.setAllowContentAccess(false);s.setMixedContentMode(WebSettings.MIXED_CONTENT_NEVER_ALLOW);s.setMediaPlaybackRequiresUserGesture(hidden);s.setUseWideViewPort(true);s.setLoadWithOverviewMode(true);
        String ua=WebSettings.getDefaultUserAgent(this).replaceAll("\\(Linux; Android[^)]*\\)","(X11; Linux x86_64)").replace("; wv","").replace(" Mobile "," ").replace("Version/4.0 ","");s.setUserAgentString(ua);userAgent=ua;w.setBackgroundColor(Color.BLACK);return w;
    }
    private boolean block(WebResourceRequest r){return r.isForMainFrame()&&!"https".equalsIgnoreCase(r.getUrl().getScheme());}
    private void dropResolver(){if(resolver!=null){stage.removeView(resolver);resolver.stopLoading();resolver.destroy();resolver=null;}}
    private void silenceWeb(){official.evaluateJavascript("document.querySelectorAll('video,audio').forEach(v=>v.pause())",null);}
    private void select(int index){
        generation++;dropResolver();player.stop();player.clearMediaItems();silenceWeb();official.stopLoading();official.loadUrl("about:blank");official.setVisibility(View.GONE);playerView.setVisibility(View.VISIBLE);current=Channel.wrap(index);fullPage=false;needsReconnect=false;adapted=false;getPreferences(0).edit().putInt("channel",current).apply();title.setText(Channel.ALL[current].name);setStatus("正在连接 · "+Channel.ALL[current].name);wake();
        if(Channel.ALL[current].direct())resolve(generation);else openOfficial(generation);
    }
    private void resolve(int token){
        webMode=false;Channel channel=Channel.ALL[current];AtomicBoolean captured=new AtomicBoolean();WebView w=webView(true);resolver=w;w.setAlpha(0);w.setImportantForAccessibility(View.IMPORTANT_FOR_ACCESSIBILITY_NO_HIDE_DESCENDANTS);stage.addView(w,0,new FrameLayout.LayoutParams(-1,-1));
        w.setWebViewClient(new WebViewClient(){
            @Override public boolean shouldOverrideUrlLoading(WebView view,WebResourceRequest r){return block(r);}
            @Override public WebResourceResponse shouldInterceptRequest(WebView view,WebResourceRequest r){String url=r.getUrl().toString();if(channel.accepts(url)&&captured.compareAndSet(false,true)){runOnUiThread(()->{if(token!=generation||!foreground)return;startNative(url,channel);});}return null;}
            @Override public void onPageFinished(WebView view,String url){if(token==generation)view.evaluateJavascript("document.querySelectorAll('video').forEach(v=>{v.muted=true;v.play().catch(()=>{});})",null);}
        });w.loadUrl(channel.page);
        handler.postDelayed(()->{if(token==generation&&!captured.get()){dropResolver();setStatus("连接超时，可点「更多」重试或打开官网模式");}},35000);
    }
    private Map<String,String> headers(Channel channel){Map<String,String> values=new java.util.HashMap<>();values.put("Referer",channel.page);values.put("Origin",Uri.parse(channel.page).getScheme()+"://"+Uri.parse(channel.page).getHost());return values;}
    private void startNative(String url,Channel channel){dropResolver();DefaultHttpDataSource.Factory factory=new DefaultHttpDataSource.Factory().setUserAgent(userAgent).setDefaultRequestProperties(headers(channel));player.setMediaSource(new HlsMediaSource.Factory(factory).createMediaSource(MediaItem.fromUri(url)));player.prepare();player.play();}
    private void openOfficial(int token){
        webMode=true;player.stop();dropResolver();playerView.setVisibility(View.GONE);official.setVisibility(View.VISIBLE);controls.bringToFront();
        official.setWebViewClient(new WebViewClient(){@Override public boolean shouldOverrideUrlLoading(WebView v,WebResourceRequest r){return block(r);}@Override public void onPageFinished(WebView v,String url){if(token==generation&&url.startsWith("https://"))adapt(token,0);}});official.loadUrl(Channel.ALL[current].page);
    }
    private boolean sameOfficialPage(){Uri expected=Uri.parse(Channel.ALL[current].page),actual=Uri.parse(official.getUrl()==null?"":official.getUrl());return expected.getHost().equals(actual.getHost())&&expected.getPath().equals(actual.getPath())&&java.util.Objects.equals(expected.getQuery(),actual.getQuery());}
    private void adapt(int token,int attempt){if(token!=generation||!webMode||fullPage||!foreground||!sameOfficialPage())return;official.evaluateJavascript(adapter,result->{if(token!=generation||!foreground)return;if(result!=null&&result.contains("\"found\":true")){adapted=true;official.evaluateJavascript("window.__tvLive.command('start',1)",null);setStatus("官网视频模式 · 若未播放，请轻触画面");}else if(attempt<35)handler.postDelayed(()->adapt(token,attempt+1),1000);else setStatus("官网播放器尚未就绪，可在「更多」显示完整官网");});}
    private void toggle(){if(needsReconnect){select(current);return;}if(webMode)official.evaluateJavascript("window.__tvLive ? window.__tvLive.command('toggle',1) : document.querySelectorAll('video').forEach(v=>v.paused?v.play().catch(()=>{}):v.pause())",null);else if(player.getMediaItemCount()==0)select(current);else if(player.isPlaying())player.pause();else player.play();}
    private void menu(){new AlertDialog.Builder(this).setTitle(Channel.ALL[current].name).setItems(new String[]{"重新连接","官网视频模式","显示完整官网","用浏览器打开官网","复制诊断信息"},(d,n)->{switch(n){case 0:select(current);break;case 1:generation++;fullPage=false;openOfficial(generation);break;case 2:fullPage=true;if(!webMode){generation++;openOfficial(generation);}official.evaluateJavascript("window.__tvLive?.command('restore')",null);break;case 3:try{startActivity(new Intent(Intent.ACTION_VIEW,Uri.parse(Channel.ALL[current].page)));}catch(ActivityNotFoundException e){Toast.makeText(this,"未找到浏览器",Toast.LENGTH_SHORT).show();}break;case 4:((android.content.ClipboardManager)getSystemService(CLIPBOARD_SERVICE)).setPrimaryClip(ClipData.newPlainText("鲸鱼电视诊断","鲸鱼电视 Android 0.1.0\nAndroid "+Build.VERSION.RELEASE+"\n频道："+Channel.ALL[current].name+"\n模式："+(webMode?"官网":"原生")+"\n状态："+status.getText()));Toast.makeText(this,"已复制诊断信息",Toast.LENGTH_SHORT).show();break;}}).show();}
    @Override protected void onPause(){needsReconnect=resolver!=null||(webMode&&!adapted&&!fullPage);foreground=false;generation++;dropResolver();player.pause();silenceWeb();official.onPause();handler.removeCallbacks(hide);super.onPause();}
    @Override protected void onResume(){super.onResume();foreground=true;if(official!=null)official.onResume();if(needsReconnect)setStatus("连接已暂停 · 点播放按钮重新连接");wake();}
    @Override protected void onDestroy(){handler.removeCallbacksAndMessages(null);dropResolver();playerView.setPlayer(null);player.release();stage.removeView(official);official.destroy();super.onDestroy();}
}
