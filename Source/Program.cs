using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Windows.Forms;
using System.Diagnostics;
using System.Threading;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Win32;

[assembly:AssemblyTitle("Снег — маленькая зима")]
[assembly:AssemblyDescription("Локальный снег поверх настоящих окон Windows")]
[assembly:AssemblyVersion("1.0.0.0")]
[assembly:AssemblyFileVersion("1.0.0.0")]
namespace Sneg {
 internal static class Program {
  [STAThread] static int Main(string[] args){
   Native.Dpi();Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
   if(args.Contains("--self-test"))return SelfTests.Run(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Tests"));
   if(args.Contains("--audio-test")){var settings=new Settings{SoundVolume=20};var world=new SnowWorld(settings,1);world.Plowing=true;using(var audio=new SoundEngine(settings,true)){audio.Update(world,true);audio.Trigger("shovel",2000);Thread.Sleep(600);string result="Открыто устройство: "+audio.Opened+"\r\nПередано PCM-блоков: "+audio.Submitted+"\r\nОшибка: "+audio.Error+"\r\nЭто проверка передачи PCM драйверу, а не подтверждение слышимости или качества звучания.\r\n";File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Tests","audio-device-results.txt"),result);return audio.Opened&&audio.Submitted>0?0:1;}}
   bool lab=Path.GetFileNameWithoutExtension(Application.ExecutablePath).IndexOf("Lab",StringComparison.OrdinalIgnoreCase)>=0||args.Contains("--lab");
   bool created;using(var mutex=new Mutex(true,"Local\\Sneg.Desktop.0.1"+(lab?".Lab":""),out created)){
    if(!created){MessageBox.Show("Снег уже работает. Откройте настройки через значок снежинки в области уведомлений.","Снег",MessageBoxButtons.OK,MessageBoxIcon.Information);return 0;}
    SnowApp app=null;try{app=new SnowApp(lab,args);if(args.Contains("--ui-preview"))app.Panel.ExportPreview(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Tests","Evidence"));Application.Run(app);return 0;}catch(Exception e){if(SnowApp.Current!=null)SnowApp.Current.Abort();try{File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"crash.txt"),e.ToString());}catch(IOException){}MessageBox.Show("Не удалось запустить снег: "+e.Message,"Снег",MessageBoxButtons.OK,MessageBoxIcon.Error);return 1;}
   }
  }
 }
 internal sealed class HotkeyWindow : NativeWindow,IDisposable {
  public Action<int> Press;
  public HotkeyWindow(){CreateHandle(new CreateParams{Caption="Sneg commands",Parent=new IntPtr(-3)});}
  protected override void WndProc(ref Message m){if(m.Msg==0x312&&Press!=null)Press(m.WParam.ToInt32());base.WndProc(ref m);}
  public void Dispose(){for(int i=1;i<=4;i++)Native.UnregisterHotKey(Handle,i);DestroyHandle();}
 }
 internal sealed class SnowApp : ApplicationContext {
  public static SnowApp Current;
  public readonly Settings Settings;public readonly SnowWorld World;public readonly WindowTracker Tracker=new WindowTracker();public bool Enabled=true,SystemPause,Exiting,Lab;
  public double FrameMs;public string Notice="";public SettingsForm Panel;public SoundEngine Audio;BrickInput brickInput;PointF lastShakePoint;bool hasShakePoint;
  readonly List<Overlay> layers=new List<Overlay>();readonly List<Form> fixtures=new List<Form>();readonly List<double> frameIntervals=new List<double>(),renderTimes=new List<double>();
  readonly Stopwatch clock=Stopwatch.StartNew();readonly System.Windows.Forms.Timer timer;readonly NotifyIcon tray;readonly HotkeyWindow commands;readonly string data,config,metrics;readonly double duration;
  double last,nextRender,nextTrack,nextStats,previousRender=-1;bool locked,rebuild,seed,autoTests,nativeTesting;int slowFrames,fastFrames;StreamWriter telemetry;Icon appIcon;
  public SnowApp(bool lab,string[] args){Current=this;Lab=lab;data=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,lab?"LabData":"Data");Directory.CreateDirectory(data);config=Path.Combine(data,"settings.xml");metrics=Path.Combine(data,"metrics.csv");Settings=Settings.Load(config);World=new SnowWorld(Settings,7261);Audio=new SoundEngine(Settings,!args.Contains("--silent-test"));World.Sound=delegate(string kind,double amount){Audio.Trigger(kind,amount);};seed=lab||args.Contains("--seed");
   autoTests=args.Contains("--native-test")||Path.GetFileNameWithoutExtension(Application.ExecutablePath).IndexOf("LabAuto",StringComparison.OrdinalIgnoreCase)>=0;if(autoTests)duration=45;int di=Array.IndexOf(args,"--duration");if(di>=0&&di+1<args.Length){double val;if(double.TryParse(args[di+1],System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out val))duration=val;}
   telemetry=new StreamWriter(metrics,false,System.Text.Encoding.UTF8);telemetry.AutoFlush=true;telemetry.WriteLine("seconds,working_set_mb,private_mb,handles,gdi,user,cpu_total_ms,render_ms,particles,balls,surfaces,tool");
   commands=new HotkeyWindow();commands.Press=HandleHotkey;SetHotkey(Settings.HotKey);
   if(!Native.RegisterHotKey(commands.Handle,3,0x4000|1|2|4,81))Notice="Аварийная клавиша занята. Используйте меню значка.";
   appIcon=MakeIcon();tray=new NotifyIcon{Icon=appIcon,Text="Снег · двойной щелчок — настройки",Visible=true};tray.DoubleClick+=delegate{ShowPanel();};tray.ContextMenuStrip=Menu();
   brickInput=new BrickInput(this);Panel=new SettingsForm(this){Icon=appIcon};Tracker.NoSupport.Add(Panel.Handle);Panel.Show();
   if(lab){var screen=Screen.PrimaryScreen.WorkingArea;var a=new LabFixture("Снег — тестовое окно А",Path.Combine(data,"fixture-A.txt"));a.StartPosition=FormStartPosition.Manual;a.Bounds=new Rectangle(screen.Left+130,screen.Top+220,650,450);fixtures.Add(a);a.Show();var b=new LabFixture("Снег — тестовое окно Б",Path.Combine(data,"fixture-B.txt"));b.StartPosition=FormStartPosition.Manual;b.Bounds=new Rectangle(screen.Left+850,screen.Top+410,600,400);fixtures.Add(b);b.Show();Panel.Location=new Point(screen.Right-Panel.Width-55,screen.Top+90);}
   RebuildLayers();SystemEvents.DisplaySettingsChanged+=DisplayChanged;SystemEvents.SessionSwitch+=SessionChanged;SystemEvents.PowerModeChanged+=PowerChanged;
   Native.timeBeginPeriod(1);last=clock.Elapsed.TotalSeconds;timer=new System.Windows.Forms.Timer{Interval=1};timer.Tick+=Tick;timer.Start();if(args.Contains("--tray"))Panel.Hide();
  }
  Icon MakeIcon(){using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("Sneg.AppIcon")){if(stream==null)throw new InvalidOperationException("Ресурс иконки приложения отсутствует");using(var icon=new Icon(stream,new Size(32,32)))return (Icon)icon.Clone();}}
  ContextMenuStrip Menu(){var m=new ContextMenuStrip();m.Items.Add("Открыть настройки",null,delegate{ShowPanel();});m.Items.Add("Снег / выключить эффект",null,delegate{ToggleEnabled();});m.Items.Add("Пауза / продолжить",null,delegate{SetPaused(!World.Paused);});m.Items.Add(new ToolStripSeparator());m.Items.Add("Снежок",null,delegate{SetTool(Tool.Snowball);});m.Items.Add("Горсть",null,delegate{SetTool(Tool.Handful);});m.Items.Add("Кирпичик",null,delegate{SetTool(Tool.Brick);});m.Items.Add("Варежка",null,delegate{SetTool(Tool.Mitten);});m.Items.Add("Лопата",null,delegate{SetTool(Tool.Shovel);});m.Items.Add("Обычная работа · без инструмента",null,delegate{SetTool(Tool.None);});m.Items.Add("Очистить весь снег",null,delegate{World.Clear();});m.Items.Add("Снегоуборщик",null,delegate{World.StartPlow();});m.Items.Add(new ToolStripSeparator());m.Items.Add("Аварийно отключить · Ctrl+Alt+Shift+Q",null,delegate{Emergency();});m.Items.Add("Выход",null,delegate{Exit();});return m;}
  public void ShowPanel(){if(Panel.IsDisposed)return;Panel.Show();Panel.WindowState=FormWindowState.Normal;Panel.Activate();}
  public void SetHotkey(int letter){Native.UnregisterHotKey(commands.Handle,1);if(!Native.RegisterHotKey(commands.Handle,1,0x4000|1|2,(uint)letter)){Notice="Ctrl+Alt+"+(char)letter+" занята. Выберите другую букву в настройках.";}else{Settings.HotKey=letter;Notice="";}}
  void HandleHotkey(int id){if(id==1)SetTool(World.Tool==Tool.None?Tool.Snowball:Tool.None);else if(id==2)SetTool(Tool.None);else if(id==3)Emergency();else if(id==4){World.ToggleBrickPin();brickInput.RecordPin();}}
  public void SetTool(Tool tool){World.CancelDrag();foreach(var l in layers)l.Cancel();Native.ReleaseCapture();Native.UnregisterHotKey(commands.Handle,2);if(tool!=Tool.None){if(!Enabled||World.Paused||SystemPause){Notice="Сначала включите эффект и снимите паузу.";tool=Tool.None;}else if(!Native.RegisterHotKey(commands.Handle,2,0x4000,27)){Notice="Escape занята. Инструмент не включён.";tool=Tool.None;}}
   World.Tool=tool;if(Panel!=null)Panel.RefreshState();Render();}
  public void SetPaused(bool value){World.Paused=value;World.ResetClock();if(value)SetTool(Tool.None);Panel.RefreshState();Render();}
  public void ToggleEnabled(){Enabled=!Enabled;if(!Enabled)SetTool(Tool.None);World.ResetClock();Panel.RefreshState();Render();}
  public void Emergency(){Enabled=false;SetTool(Tool.None);World.ResetClock();Notice="Эффект отключён. Мышь освобождена.";Panel.RefreshState();Render();}
  void RebuildLayers(){SetTool(Tool.None);foreach(var l in layers){Tracker.Excluded.Remove(l.Handle);l.Close();l.Dispose();}layers.Clear();World.Monitors=Screen.AllScreens.Select(s=>s.Bounds).ToList();
   foreach(var s in Screen.AllScreens){var visual=new Overlay(this,s.Bounds,false);layers.Add(visual);visual.Show();var input=new Overlay(this,s.Bounds,true);layers.Add(input);input.Show();}
   Tracker.Refresh();World.Sync(Tracker.Windows,Screen.AllScreens.Select(s=>s.WorkingArea).ToList());Render();}
  void DisplayChanged(object sender,EventArgs e){if(Panel!=null&&!Panel.IsDisposed)Panel.BeginInvoke(new Action(delegate{rebuild=true;}));}
  void SessionChanged(object sender,SessionSwitchEventArgs e){if(Panel!=null&&!Panel.IsDisposed)Panel.BeginInvoke(new Action(delegate{if(e.Reason==SessionSwitchReason.SessionLock)locked=true;if(e.Reason==SessionSwitchReason.SessionUnlock)locked=false;World.ResetClock();}));}
  void PowerChanged(object sender,PowerModeChangedEventArgs e){if(Panel!=null&&!Panel.IsDisposed)Panel.BeginInvoke(new Action(delegate{World.ResetClock();last=clock.Elapsed.TotalSeconds;}));}
  void Tick(object sender,EventArgs e){try{double now=clock.Elapsed.TotalSeconds,elapsed=now-last;last=now;if(duration>0&&now>=duration){Exit();return;}if(autoTests&&now>1){autoTests=false;RunNativeTests();return;}if(rebuild){rebuild=false;RebuildLayers();}
    if(now>=nextTrack){nextTrack=now+.08;Tracker.Refresh();World.Sync(Tracker.Windows,Screen.AllScreens.Select(s=>s.WorkingArea).ToList());if(seed){World.Seed();seed=false;}bool pause=locked||Settings.PauseFullscreen&&Tracker.Fullscreen();if(pause!=SystemPause){SystemPause=pause;SetTool(Tool.None);World.ResetClock();}}
    if(Enabled&&!SystemPause){World.Advance(elapsed);foreach(var l in layers)if(l.Input)l.PollDrag();}else World.ResetClock();
    Audio.Update(World,Enabled&&!SystemPause);if(Audio.Error.Length>0)Notice="Звук недоступен: "+Audio.Error;
    Native.Pt pointer;bool hasPointer=Native.GetCursorPos(out pointer);if(hasPointer){World.Pointer=new PointF(pointer.X,pointer.Y);if(hasShakePoint&&Enabled&&!SystemPause&&!(Panel.Visible&&Panel.Bounds.Contains(pointer.X,pointer.Y)))World.ShakeSnow(lastShakePoint,World.Pointer,elapsed);lastShakePoint=World.Pointer;hasShakePoint=true;}else hasShakePoint=false;brickInput.Sync(commands.Handle);
    World.ShowTool=hasPointer&&Enabled&&!SystemPause&&!World.Paused&&(World.Tool==Tool.Mitten||World.Tool==Tool.Shovel||World.Tool==Tool.Handful||World.Tool==Tool.Brick)&&!(Panel.Visible&&Panel.Bounds.Contains(pointer.X,pointer.Y));
    if(now>=nextRender){nextRender=now+1.0/Settings.Fps;if(previousRender>=0&&Enabled&&!SystemPause&&!World.Paused){frameIntervals.Add((now-previousRender)*1000);if(frameIntervals.Count>432000)frameIntervals.RemoveRange(0,10000);}previousRender=now;var watch=Stopwatch.StartNew();Render();FrameMs=watch.Elapsed.TotalMilliseconds;renderTimes.Add(FrameMs);if(renderTimes.Count>432000)renderTimes.RemoveRange(0,10000);
     if(FrameMs>1000.0/Settings.Fps){fastFrames=0;if(++slowFrames>45){World.EffectiveLimit=Math.Max(150,(int)(World.EffectiveLimit*.85));if(World.Flakes.Count>World.EffectiveLimit)World.TrimDecorative();slowFrames=0;}}else{slowFrames=0;if(++fastFrames>600){World.EffectiveLimit=Math.Min(Settings.MaxParticles,World.EffectiveLimit+100);fastFrames=0;}}World.EffectiveLimit=Math.Min(Settings.MaxParticles,World.EffectiveLimit);
    }
    if(now>=nextStats){nextStats=now+1;Panel.RefreshState();WriteTelemetry(now);}
   }catch(Exception error){if(brickInput!=null)brickInput.Dispose();if(Audio!=null)Audio.Update(World,false);Enabled=false;World.Tool=Tool.None;World.CancelDrag();foreach(var l in layers){l.Cancel();l.Hide();}Native.ReleaseCapture();Native.UnregisterHotKey(commands.Handle,2);Notice="Эффект остановлен: "+error.Message;try{File.AppendAllText(Path.Combine(data,"errors.txt"),DateTime.UtcNow.ToString("o")+" "+error+Environment.NewLine);}catch(IOException){}Panel.RefreshState();timer.Stop();}}
  void Render(){foreach(var l in layers)if(!l.IsDisposed)l.Render();}
  void WriteTelemetry(double now){var p=Process.GetCurrentProcess();telemetry.WriteLine(string.Join(",",new[]{now.ToString("F2",System.Globalization.CultureInfo.InvariantCulture),(p.WorkingSet64/1048576.0).ToString("F2",System.Globalization.CultureInfo.InvariantCulture),(p.PrivateMemorySize64/1048576.0).ToString("F2",System.Globalization.CultureInfo.InvariantCulture),p.HandleCount.ToString(),Native.GetGuiResources(p.Handle,0).ToString(),Native.GetGuiResources(p.Handle,1).ToString(),p.TotalProcessorTime.TotalMilliseconds.ToString("F0",System.Globalization.CultureInfo.InvariantCulture),FrameMs.ToString("F2",System.Globalization.CultureInfo.InvariantCulture),World.Flakes.Count.ToString(),World.Balls.Count.ToString(),World.Surfaces.Count.ToString(),World.Tool.ToString()}));p.Dispose();}
  public void Save(){try{Settings.Save(config);}catch(Exception e){Notice="Настройки не сохранены: "+e.Message;}}
  public void DemoBrick(){if(!Lab||fixtures.Count==0)return;SetTool(Tool.Brick);var b=new SnowBrick{X=fixtures[0].Left+180,Y=fixtures[0].Top-75,Area=SnowWorld.BrickCapacity,Held=true};World.Bricks.Add(b);World.HeldBrick=b;Notice="Проверка: колёсико поворачивает блок, F меняет закрепление. Escape отпускает его.";Panel.RefreshState();}
  public void DemoSnowman(){if(!Lab||fixtures.Count==0)return;Tracker.Refresh();World.Sync(Tracker.Windows,Screen.AllScreens.Select(display=>display.WorkingArea).ToList());var s=World.Surfaces.Find(v=>v.Window==fixtures[0].Handle);if(s==null)return;double x=s.Left+s.Width*.62;var bottom=new Ball{X=x,Y=s.Y-s.Height(x)-30,Area=Math.PI*900,Support=s};var middle=new Ball{X=x,Y=bottom.Y-30-24+1,Area=Math.PI*576,Support=s,OnBall=bottom};var head=new Ball{X=x,Y=middle.Y-24-25+1,Area=Math.PI*625,Support=s,OnBall=middle,Face=true};World.Balls.Add(bottom);World.Balls.Add(middle);World.Balls.Add(head);}
  public void RunNativeTests(){if(nativeTesting)return;nativeTesting=true;timer.Stop();SetTool(Tool.None);try{NativeTests.Run(this,Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Tests"));Notice="Проверка 100 циклов завершена. Отчёт — Tests/native-results.md";}catch(Exception e){Notice="Ошибка проверки: "+e.Message;}finally{nativeTesting=false;last=clock.Elapsed.TotalSeconds;World.ResetClock();if(!Exiting)timer.Start();Panel.RefreshState();}}
  public void Abort(){Exiting=true;if(Audio!=null)Audio.Dispose();if(brickInput!=null)brickInput.Dispose();if(timer!=null)timer.Stop();foreach(var l in layers){l.Cancel();l.Close();l.Dispose();}foreach(var f in fixtures){f.Close();f.Dispose();}if(Panel!=null)Panel.Close();if(commands!=null)commands.Dispose();if(tray!=null){tray.Visible=false;tray.Dispose();}if(telemetry!=null)telemetry.Dispose();Native.ReleaseCapture();Native.timeEndPeriod(1);}
  public void Exit(){if(Exiting)return;Exiting=true;if(Audio!=null)Audio.Dispose();if(brickInput!=null)brickInput.Dispose();if(timer!=null)timer.Stop();World.CancelDrag();foreach(var l in layers)l.Cancel();Native.ReleaseCapture();Save();
   if(telemetry!=null){WriteTelemetry(clock.Elapsed.TotalSeconds);telemetry.Dispose();telemetry=null;}
   var text="Сборка: 1.0.0\r\nДлительность: "+clock.Elapsed.TotalSeconds.ToString("F1")+" с\r\nЭкран: "+string.Join("; ",Screen.AllScreens.Select(s=>s.Bounds.ToString()))+"\r\nОС: "+Environment.OSVersion+"\r\nCPU логических: "+Environment.ProcessorCount+"\r\nКадров: "+frameIntervals.Count+"\r\nP95 интервала кадра: "+P95(frameIntervals).ToString("F2")+" мс\r\nP95 отрисовки: "+P95(renderTimes).ToString("F2")+" мс\r\nНе является доказательством испытания на чистой системе или нескольких мониторах.\r\n";try{File.WriteAllText(Path.Combine(data,"session.txt"),text,System.Text.Encoding.UTF8);}catch(IOException){}
   foreach(var l in layers){l.Close();l.Dispose();}layers.Clear();foreach(var f in fixtures)f.Close();if(Panel!=null)Panel.Close();if(tray!=null){tray.Visible=false;tray.Dispose();}commands.Dispose();SystemEvents.DisplaySettingsChanged-=DisplayChanged;SystemEvents.SessionSwitch-=SessionChanged;SystemEvents.PowerModeChanged-=PowerChanged;Native.timeEndPeriod(1);if(appIcon!=null)appIcon.Dispose();ExitThread();}
  static double P95(List<double> xs){if(xs.Count==0)return 0;var a=xs.OrderBy(x=>x).ToArray();return a[Math.Min(a.Length-1,(int)Math.Ceiling(a.Length*.95)-1)];}
  protected override void Dispose(bool disposing){if(disposing&&!Exiting)Exit();base.Dispose(disposing);}
 }
 internal sealed class LabFixture:Form {
  int count;Label counter;readonly string log;
  public LabFixture(string title,string logPath){log=logPath;Text=title;Font=new Font("Segoe UI",11);BackColor=Color.FromArgb(235,243,250);var p=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,Padding=new Padding(22),WrapContents=false};Controls.Add(p);p.Controls.Add(new Label{Text="Настоящее окно для проверки снега",AutoSize=true});var bt=new Button{Text="Проверить обычный клик",Width=340,Height=48};bt.Click+=delegate{count++;counter.Text="Обычных кликов: "+count;Record();};p.Controls.Add(bt);counter=new Label{Text="Обычных кликов: 0",AutoSize=true};p.Controls.Add(counter);p.Controls.Add(new TextBox{Multiline=true,AcceptsReturn=true,ScrollBars=ScrollBars.Vertical,Width=480,Height=200,Text="Тестовое поле. Здесь можно печатать, выделять и прокручивать текст."});Move+=delegate{Record();};Resize+=delegate{Record();};Shown+=delegate{Record();};}
  void Record(){try{File.WriteAllText(log,"Clicks="+count+"\r\nBounds="+Bounds+"\r\nState="+WindowState);}catch(IOException){}catch(UnauthorizedAccessException){}}
 }
}
