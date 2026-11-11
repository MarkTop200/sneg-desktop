using System;
using System.IO;
using System.Drawing;
using System.Windows.Forms;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace Sneg {
 internal static class NativeTests {
  public static void Run(SnowApp app,string dir){Directory.CreateDirectory(dir);int passed=0,failed=0;var report=new StringBuilder("# Испытания настоящих окон\r\n\r\nДата UTC: "+DateTime.UtcNow.ToString("o")+"\r\n\r\nПроверяются собственные тестовые окна через реальную оконную систему. Действия пользователя мышью, чужие приложения, DPI и сон этим сценарием не проверяются.\r\n\r\n");
   var process=Process.GetCurrentProcess();int gdi=Native.GetGuiResources(process.Handle,0),user=Native.GetGuiResources(process.Handle,1);var sw=Stopwatch.StartNew();
   for(int cycle=0;cycle<100;cycle++){
    using(var f=new Form{Text="Снег — временное испытательное окно",StartPosition=FormStartPosition.Manual,Bounds=new Rectangle(800,600,420,260),ShowInTaskbar=true}){
     f.Show();f.Update();Refresh(app);var surface=app.World.Surfaces.Find(s=>s.Window==f.Handle);for(int retry=0;surface==null&&retry<25;retry++){System.Threading.Thread.Sleep(10);Refresh(app);surface=app.World.Surfaces.Find(s=>s.Window==f.Handle);}Check(surface!=null,"Создание (до 250 мс на готовность DWM)",cycle,report,ref passed,ref failed);if(surface==null){int cloak;Native.DwmInt(f.Handle,14,out cloak,4);var found=app.Tracker.Windows.Find(v=>v.Handle==f.Handle);report.Append("  Диагностика: handle="+f.Handle+"; visible="+Native.IsWindowVisible(f.Handle)+"; iconic="+Native.IsIconic(f.Handle)+"; bounds="+Native.Bounds(f.Handle)+"; style="+Native.GetWindowLong(f.Handle,-16).ToString("X")+"; ex="+Native.GetWindowLong(f.Handle,-20).ToString("X")+"; cloaked="+cloak+"; excluded="+app.Tracker.Excluded.Contains(f.Handle)+"; noSupport="+app.Tracker.NoSupport.Contains(f.Handle)+"; enumerated="+(found!=null)+"; eligible="+(found!=null&&found.Surface)+".\r\n");continue;}
     surface.Add(surface.Left+80,500);var ball=new Ball{X=surface.Left+80,Y=surface.Y-12,Area=Math.PI*144,Support=surface};app.World.Balls.Add(ball);double oldX=ball.X,oldY=ball.Y;
     f.Location=new Point(880,650);Refresh(app);Check(Math.Abs(ball.X-oldX)<2&&Math.Abs(ball.Y-oldY)<2&&ball.Support==null&&surface.Area<.01&&app.World.Flakes.Any(v=>v.Debris&&v.IgnoreSurface==surface.Id),"Осыпание снега и освобождение шара при перемещении",cycle,report,ref passed,ref failed);
     // Test minimize/restore independently: put the ball back on the new ledge.
     ball.Support=surface;ball.X=surface.Left+80;ball.Y=surface.Y-ball.Radius;
     f.Width=340;Refresh(app);Check(Math.Abs(surface.Width-Native.Bounds(f.Handle).Width)<2,"Ширина",cycle,report,ref passed,ref failed);
     f.WindowState=FormWindowState.Minimized;Refresh(app);Check(!surface.Active&&!ball.Visible,"Сворачивание",cycle,report,ref passed,ref failed);
     f.WindowState=FormWindowState.Normal;Refresh(app);Check(surface.Active&&ball.Visible,"Восстановление",cycle,report,ref passed,ref failed);
     f.Close();Refresh(app);Check(!app.World.Surfaces.Contains(surface)&&ball.Support==null,"Закрытие и освобождение опоры",cycle,report,ref passed,ref failed);app.World.Balls.Remove(ball);
    }
    app.SetTool(Tool.Snowball);if(app.World.Tool!=Tool.Snowball&&cycle==0)report.Append("  Причина включения: "+app.Notice+"; enabled="+app.Enabled+"; paused="+app.World.Paused+"; systemPause="+app.SystemPause+".\r\n");Check(app.World.Tool==Tool.Snowball,"Включение инструмента",cycle,report,ref passed,ref failed);app.SetTool(Tool.None);Check(app.World.Tool==Tool.None&&app.World.Held==null,"Отключение инструмента",cycle,report,ref passed,ref failed);
   }
   GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();int afterGdi=Native.GetGuiResources(process.Handle,0),afterUser=Native.GetGuiResources(process.Handle,1);
   report.Append("Выполнено 100 циклов: создание → перемещение → изменение ширины → сворачивание → восстановление → закрытие, включение/выключение настоящего инструмента.\r\n\r\nПройдено проверок: "+passed+"; не пройдено: "+failed+".\r\nВремя: "+sw.Elapsed.TotalSeconds.ToString("F2")+" с.\r\nGDI: "+gdi+" → "+afterGdi+"; USER: "+user+" → "+afterUser+".\r\n\r\nЭто короткий цикл, а не двухчасовое испытание утечек.\r\n");
   File.WriteAllText(Path.Combine(dir,"native-results.md"),report.ToString(),Encoding.UTF8);process.Dispose();
  }
  static void Refresh(SnowApp app){Application.DoEvents();app.Tracker.Refresh();app.World.Sync(app.Tracker.Windows,Screen.AllScreens.Select(s=>s.WorkingArea).ToList());}
  static void Check(bool ok,string stage,int cycle,StringBuilder log,ref int passed,ref int failed){if(ok)passed++;else{failed++;log.Append("- Не пройден: цикл "+(cycle+1)+", "+stage+".\r\n");}}
 }
}
