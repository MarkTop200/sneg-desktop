using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.ComponentModel;

namespace Sneg {
 internal sealed class LayerBuffer : IDisposable {
  public Bitmap Bitmap;public Graphics G;public IntPtr DC;IntPtr dib,old;
  public LayerBuffer(int width,int height){
   DC=Native.CreateCompatibleDC(IntPtr.Zero);var info=new Native.BitmapInfo{Size=40,Width=width,Height=-height,Planes=1,Bits=32};IntPtr bits;
   dib=Native.CreateDIBSection(DC,ref info,0,out bits,IntPtr.Zero,0);if(dib==IntPtr.Zero){Native.DeleteDC(DC);throw new Win32Exception();}old=Native.SelectObject(DC,dib);
   Bitmap=new Bitmap(width,height,width*4,PixelFormat.Format32bppPArgb,bits);G=Graphics.FromImage(Bitmap);G.SmoothingMode=SmoothingMode.AntiAlias;G.PixelOffsetMode=PixelOffsetMode.HighQuality;
  }
  public void Dispose(){if(G!=null)G.Dispose();if(Bitmap!=null)Bitmap.Dispose();if(DC!=IntPtr.Zero){Native.SelectObject(DC,old);Native.DeleteObject(dib);Native.DeleteDC(DC);DC=IntPtr.Zero;}}
 }
 internal sealed class Overlay : Form {
  public readonly bool Input;public readonly Rectangle ScreenBounds;public readonly SnowApp App;LayerBuffer buffer;bool dragging,wasBlank;IntPtr registeredHandle;
  public Overlay(SnowApp app,Rectangle bounds,bool input){App=app;Input=input;ScreenBounds=bounds;Text=input?"Снег — инструмент":"Снег — прозрачный слой";FormBorderStyle=FormBorderStyle.None;ShowInTaskbar=app.Lab&&input;StartPosition=FormStartPosition.Manual;Bounds=bounds;AutoScaleMode=AutoScaleMode.None;TopMost=true;}
  protected override bool ShowWithoutActivation{get{return true;}}
  protected override CreateParams CreateParams{get{var p=base.CreateParams;p.ExStyle|=Native.Layered;if(!(App!=null&&App.Lab&&Input))p.ExStyle|=Native.ToolWindow|Native.NoActivate;if(!Input)p.ExStyle|=Native.Transparent;return p;}}
  protected override void OnHandleCreated(EventArgs e){base.OnHandleCreated(e);registeredHandle=Handle;App.Tracker.Excluded.Add(registeredHandle);if(buffer!=null)buffer.Dispose();buffer=new LayerBuffer(Width,Height);wasBlank=false;}
  protected override void OnHandleDestroyed(EventArgs e){if(App!=null)App.Tracker.Excluded.Remove(registeredHandle);registeredHandle=IntPtr.Zero;if(buffer!=null){buffer.Dispose();buffer=null;}base.OnHandleDestroyed(e);}
  public void Render(){if(buffer==null)return;bool blank=!App.Enabled||App.SystemPause||Input&&(App.World.Paused||App.World.Tool==Tool.None&&App.World.Bricks.Count==0);if(blank&&wasBlank)return;wasBlank=blank;var g=buffer.G;g.ResetTransform();g.ResetClip();g.CompositingMode=CompositingMode.SourceCopy;g.Clear(Color.Transparent);g.CompositingMode=CompositingMode.SourceOver;g.TranslateTransform(-ScreenBounds.X,-ScreenBounds.Y);g.SetClip(ScreenBounds);
   if(App.Enabled&&!App.SystemPause&&(!Input||(App.World.Tool!=Tool.None||App.World.Bricks.Count>0)&&!App.World.Paused))ScenePainter.Draw(g,App.World,Input,App.Settings.Diagnostics&&!Input,App.FrameMs);
   g.Flush();var point=new Native.Pt(ScreenBounds.X,ScreenBounds.Y);var size=new Native.Sz(Width,Height);var zero=new Native.Pt(0,0);var blend=new Native.Blend{Op=0,Alpha=255,Format=1};
   if(!Native.UpdateLayeredWindow(Handle,IntPtr.Zero,ref point,ref size,buffer.DC,ref zero,0,ref blend,2))throw new Win32Exception(Marshal.GetLastWin32Error(),"Не удалось обновить прозрачный слой");
  }
  protected override void WndProc(ref Message m){if(m.Msg==0x21){m.Result=new IntPtr(3);return;}base.WndProc(ref m);}
  protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);if(!Input||e.Button!=MouseButtons.Left)return;App.World.BeginDrag(new PointF(ScreenBounds.X+e.X,ScreenBounds.Y+e.Y));dragging=true;Capture=true;}
  protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);if(dragging)App.World.Drag(new PointF(ScreenBounds.X+e.X,ScreenBounds.Y+e.Y));}
  public void PollDrag(){if(!dragging)return;Native.Pt p;Native.GetCursorPos(out p);if((Native.GetAsyncKeyState(1)&0x8000)==0){App.World.EndDrag(new PointF(p.X,p.Y));dragging=false;Capture=false;}else App.World.Drag(new PointF(p.X,p.Y));}
  protected override void OnMouseUp(MouseEventArgs e){base.OnMouseUp(e);if(dragging&&e.Button==MouseButtons.Left){App.World.EndDrag(new PointF(ScreenBounds.X+e.X,ScreenBounds.Y+e.Y));dragging=false;Capture=false;}}
  public void Cancel(){dragging=false;Capture=false;}
  protected override void Dispose(bool disposing){if(disposing){Cancel();if(buffer!=null){buffer.Dispose();buffer=null;}}base.Dispose(disposing);}
 }
 internal static class ScenePainter {
  static readonly SolidBrush Hit=new SolidBrush(Color.FromArgb(1,255,255,255));
  internal static GraphicsPath SnowPath(Surface s){
   var path=new GraphicsPath();if(s.Area<.01)return path;
   var xs=new System.Collections.Generic.List<double>{s.Left,s.Left+s.Width};
   for(int i=0;i<s.Heights.Length;i++)xs.Add(s.Left+(i+.5)*s.Cell);
   // Extra samples resolve the rounded end caps without creating triangular tips.
   if(!s.Floor)for(int j=1;j<=7;j++){double offset=j*2;if(offset<s.Width/2){xs.Add(s.Left+offset);xs.Add(s.Left+s.Width-offset);}}
   xs.Sort();var pts=new System.Collections.Generic.List<PointF>();double last=double.NaN;
   foreach(double x in xs){if(!double.IsNaN(last)&&Math.Abs(x-last)<.01)continue;pts.Add(new PointF((float)x,(float)(s.Y-s.Height(x))));last=x;}
   float left=(float)s.Left,right=(float)(s.Left+s.Width),bottom=(float)s.Y;
   path.StartFigure();
   for(int i=0;i<pts.Count-1;i++){
    PointF a=pts[i],b=pts[i+1],before=pts[Math.Max(0,i-1)],after=pts[Math.Min(pts.Count-1,i+2)];
    float dx=b.X-a.X,lo=Math.Min(a.Y,b.Y),hi=Math.Max(a.Y,b.Y);
    float m1=(b.Y-before.Y)/Math.Max(.01f,b.X-before.X),m2=(after.Y-a.Y)/Math.Max(.01f,after.X-a.X);
    float c1=Math.Max(lo,Math.Min(hi,a.Y+m1*dx/3)),c2=Math.Max(lo,Math.Min(hi,b.Y-m2*dx/3));
    path.AddBezier(a,new PointF(a.X+dx/3,c1),new PointF(b.X-dx/3,c2),b);
   }
   if(s.Floor){path.AddLine(right,pts[pts.Count-1].Y,right,bottom);path.AddLine(right,bottom,left,bottom);}
   else{
    float rd=(float)s.RightWrap,ld=(float)s.LeftWrap,span=(float)Math.Min(26,s.Width/4);
    // A thin mantle follows the outer rounded corner and tapers down the frame.
    if(rd>.1){path.AddBezier(right,pts[pts.Count-1].Y,right+8,bottom-1,right+5,bottom+rd-2,right+3,bottom+rd);path.AddBezier(right+3,bottom+rd,right+1,bottom+rd*.2f,right-8,bottom-2,right-span,bottom);}else path.AddLine(right,pts[pts.Count-1].Y,right,bottom);
    path.AddLine(right-span,bottom,left+span,bottom);
    if(ld>.1){path.AddBezier(left+span,bottom,left+8,bottom-2,left-1,bottom+ld*.2f,left-3,bottom+ld);path.AddBezier(left-3,bottom+ld,left-5,bottom+ld-2,left-8,bottom-1,left,pts[0].Y);}else path.AddLine(left+span,bottom,left,pts[0].Y);
   }
   path.CloseFigure();return path;
  }
  public static void Draw(Graphics g,SnowWorld w,bool input,bool debug,double frame){if(input&&w.Tool==Tool.None){foreach(var brick in w.Bricks)BrickArt.Draw(g,brick,true,false);return;}
   if(!input){foreach(var f in w.Flakes){if(f.Clump)SnowArt.Heap(g,f);else using(var b=new SolidBrush(Color.FromArgb(f.Alpha,244,250,255)))g.FillEllipse(b,(float)(f.X-f.Radius),(float)(f.Y-f.Radius),(float)(f.Radius*2),(float)(f.Radius*2));}}
   foreach(var s in w.Surfaces)if(s.Active){var state=g.Save();foreach(var r in s.Occluders)g.ExcludeClip(r);
    using(var path=SnowPath(s)){if(path.PointCount>0){if(input)g.FillPath(Hit,path);else SnowArt.Drift(g,path,s);}}
    if(debug){using(var pen=new Pen(Color.FromArgb(170,87,214,255),1))g.DrawLine(pen,(float)s.Left,(float)s.Y,(float)(s.Left+s.Width),(float)s.Y);}
    g.Restore(state);
   }
   foreach(var b in w.Balls)if(b.Visible&&b.Radius>=1){var state=g.Save();if(b.Support!=null)foreach(var r in b.Support.Occluders)g.ExcludeClip(r);float x=(float)(b.X-b.Radius),y=(float)(b.Y-b.Radius),d=(float)b.Radius*2;
    if(input)g.FillEllipse(Hit,x,y,d,d);else if(b.Loose)SnowArt.Heap(g,new Flake{X=b.X,Y=b.Y,Radius=b.Radius,Phase=2});else SnowArt.Sphere(g,(float)b.X,(float)b.Y,(float)b.Radius);
    g.Restore(state);
   }
   foreach(var brick in w.Bricks)BrickArt.Draw(g,brick,input,w.Tool==Tool.Brick);
   if(!input)foreach(var b in w.Balls)if(b.Face&&b.Visible&&b.OnBall!=null){var state=g.Save();if(b.Support!=null)foreach(var r in b.Support.Occluders)g.ExcludeClip(r);SnowArt.Snowman(g,b);g.Restore(state);}
   if(!input&&w.Plowing)foreach(var s in w.Surfaces)if(s.Floor){if(s.PlowBank>1&&w.PlowX+w.PlowDirection*48>=s.Left&&w.PlowX+w.PlowDirection*48<s.Left+s.Width){double r=Math.Min(26,5+Math.Sqrt(s.PlowBank)*.13);SnowArt.Heap(g,new Flake{X=w.PlowX+w.PlowDirection*56,Y=s.Y-r*.65,Radius=r,Phase=1,Spin=0});}SnowArt.Vehicle(g,(float)w.PlowX,(float)s.Y,w.Time,w.PlowDirection);}
   if(!input)ToolArt.DrawPointer(g,w);
   if(debug&&!input&&w.Monitors.Count>0){var r=w.Monitors[0];using(var bg=new SolidBrush(Color.FromArgb(210,15,28,48)))g.FillRectangle(bg,r.Left+22,r.Top+45,390,78);using(var font=new Font("Segoe UI",10))g.DrawString(string.Format("СНЕГ · {0:F1} мс · частиц {1}/{2}\nСнежков {3} · поверхностей {4} · {5:F0} МБ\nИнструмент: {6}",frame,w.Flakes.Count,w.EffectiveLimit,w.Balls.Count,w.Surfaces.Count,System.Diagnostics.Process.GetCurrentProcess().WorkingSet64/1048576.0,w.Tool),font,Brushes.White,r.Left+32,r.Top+52);}
  }
 }
}
