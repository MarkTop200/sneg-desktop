using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Collections.Generic;

namespace Sneg {
 internal sealed class SnowBrick {
  public double X,Y,VX,VY,Angle,Area,Age;public bool Held,Pinned;public Surface Support;public SnowBrick OnBrick;
  public double Width {get{return Math.Sqrt(Math.Max(0,Area)*2.15);}}
  public double Height {get{return Width/2.15;}}
  public double HalfX {get{double a=Angle*Math.PI/180;return (Math.Abs(Math.Cos(a))*Width+Math.Abs(Math.Sin(a))*Height)/2;}}
  public double HalfY {get{double a=Angle*Math.PI/180;return (Math.Abs(Math.Sin(a))*Width+Math.Abs(Math.Cos(a))*Height)/2;}}
  public bool Contains(PointF p){double a=-Angle*Math.PI/180,dx=p.X-X,dy=p.Y-Y;return Math.Abs(dx*Math.Cos(a)-dy*Math.Sin(a))<=Width/2&&Math.Abs(dx*Math.Sin(a)+dy*Math.Cos(a))<=Height/2;}
 }
 internal sealed partial class SnowWorld {
  public readonly List<SnowBrick> Bricks=new List<SnowBrick>();public SnowBrick HeldBrick;public int PassiveBrickGrabs;
  public const double BrickCapacity=72*34;
  void BeginBrick(PointF p){HeldBrick=Bricks.LastOrDefault(b=>b.Contains(p));if(HeldBrick!=null&&Tool==Tool.None)PassiveBrickGrabs++;if(HeldBrick==null){if(Bricks.Count>=60||PickSnow(p)==null)return;HeldBrick=new SnowBrick{X=p.X,Y=p.Y,Held=true};Bricks.Add(HeldBrick);}HeldBrick.Held=true;HeldBrick.Support=null;HeldBrick.OnBrick=null;HeldBrick.VX=HeldBrick.VY=0;Drag(p);}
  void CollectBrick(double x,double y){if(HeldBrick==null)return;foreach(var s in Surfaces){if(!s.Active||x<s.Left||x>=s.Left+s.Width||!s.Visible(x,Math.Min(y,s.Y-1))||Math.Abs(y-(s.Y-s.Height(x)))>40)continue;double q=s.Take(x,24,Math.Max(0,BrickCapacity-HeldBrick.Area));HeldBrick.Area+=q;if(q>0&&Sound!=null)Sound("shovel",q);}}
  void DropBrick(bool cancel){if(HeldBrick==null)return;var b=HeldBrick;HeldBrick=null;b.Held=false;if(b.Area<20){Bricks.Remove(b);Scatter(b.Area,b.X,b.Y,0,0,null);return;}if(!cancel&&!b.Pinned&&Time-lastMoveTime<=.18){b.VX=lastThrowVX;b.VY=lastThrowVY;}else b.VX=b.VY=0;}
  public void RotateBrick(int wheelDelta){if(HeldBrick==null)return;HeldBrick.Angle=((HeldBrick.Angle+wheelDelta/120.0*15)%360+360)%360;}
  public void ToggleBrickPin(){if(HeldBrick==null)return;HeldBrick.Pinned=!HeldBrick.Pinned;HeldBrick.VX=HeldBrick.VY=0;}
  void UpdateBricks(double dt){foreach(var b in Bricks.OrderByDescending(v=>v.Y).ToArray()){
   b.Age+=dt;if(b.Held||b.Pinned)continue;
   if(b.OnBrick!=null){var below=b.OnBrick;if(!Bricks.Contains(below)||below.Held||(!below.Pinned&&below.Support==null&&below.OnBrick==null)||Math.Abs(b.X-below.X)>b.HalfX+below.HalfX-2)b.OnBrick=null;else{b.Y=below.Y-below.HalfY-b.HalfY;b.VX=b.VY=0;continue;}}
   if(b.Support!=null){var support=b.Support;if(!Surfaces.Contains(support)||!support.Active||b.X<support.Left||b.X>=support.Left+support.Width)b.Support=null;else{b.Y=support.Y-support.Height(b.X)-b.HalfY;b.VY=0;b.VX*=Math.Exp(-5*dt);b.X+=b.VX*dt;continue;}}
   double oy=b.Y;b.VY=Math.Min(1800,b.VY+550*dt);double nx=b.X+b.VX*dt,ny=b.Y+b.VY*dt,hit=2,levelHit=0;Surface landing=null;SnowBrick block=null;
   foreach(var s in Surfaces)if(s.Active){double level=s.Y-s.Height(nx)-b.HalfY;if(oy<=level+.1&&ny>=level&&ny>oy){double t=Math.Max(0,(level-oy)/(ny-oy)),x=b.X+(nx-b.X)*t;if(x>=s.Left&&x<s.Left+s.Width&&(s.Floor||s.Visible(x,level+b.HalfY-1))&&t<hit){hit=t;landing=s;block=null;levelHit=level;}}}
   foreach(var other in Bricks)if(other!=b&&!other.Held&&(other.Pinned||other.Support!=null||other.OnBrick!=null)){double level=other.Y-other.HalfY-b.HalfY;if(oy<=level+.1&&ny>=level&&ny>oy){double t=Math.Max(0,(level-oy)/(ny-oy)),x=b.X+(nx-b.X)*t;if(Math.Abs(x-other.X)<(b.HalfX+other.HalfX)*.8&&t<hit){hit=t;landing=null;block=other;levelHit=level;}}}
   b.X=hit<=1?b.X+(nx-b.X)*hit:nx;b.Y=hit<=1?levelHit:ny;if(hit<=1){b.Support=landing;b.OnBrick=block;b.VY=0;b.VX*=.35;}
  }double bottom=Monitors.Count==0?2500:Monitors.Max(r=>r.Bottom)+300;Bricks.RemoveAll(b=>!b.Held&&!b.Pinned&&b.Y-b.HalfY>bottom);}
  public void ShakeSnow(PointF from,PointF to,double elapsed){if(!Settings.MouseShake||Tool!=Tool.None||HeldBrick!=null||Paused||elapsed<=0||elapsed>.25)return;double dx=to.X-from.X,dy=to.Y-from.Y,distance=Math.Sqrt(dx*dx+dy*dy);if(distance<1||distance/elapsed<40)return;int steps=Math.Min(80,Math.Max(1,(int)Math.Ceiling(distance/8)));
   for(int i=1;i<=steps;i++){double x=from.X+dx*i/steps,y=from.Y+dy*i/steps;foreach(var s in Surfaces){if(!s.Active||x<s.Left||x>=s.Left+s.Width||!s.Visible(x,Math.Min(y,s.Y-1)))continue;double h=s.Height(x);if(h<.5||Math.Abs(y-(s.Y-h))>20&&!s.WrapContains(x,y))continue;if(Flakes.Count>=EffectiveLimit)return;double q=s.Take(x,15,Math.Min(250,20+distance/steps*12));if(q>0){Flakes.Add(CreateClump(q,x,s.Y-h-8,s.Id,Math.Max(-180,Math.Min(180,dx/elapsed*.2)),-30));}}}
  }
 }
 internal static class BrickArt {
  internal static readonly Bitmap Icon=MakeIcon();
  static Bitmap MakeIcon(){var image=new Bitmap(40,40);using(var g=Graphics.FromImage(image)){g.SmoothingMode=SmoothingMode.AntiAlias;DrawMould(g,20,24,34,18,0);}return image;}
  internal static void DrawMould(Graphics g,float x,float y,float width,float height,float angle){var state=g.Save();g.TranslateTransform(x,y);g.RotateTransform(angle);using(var p=PanelTheme.Round(new RectangleF(-width/2-5,-height/2-4,width+10,height+8),3)){using(var b=new SolidBrush(Color.FromArgb(105,32,166,224)))g.FillPath(b,p);using(var pen=new Pen(Color.FromArgb(19,142,213),3))g.DrawPath(pen,p);}using(var p=new Pen(Color.FromArgb(58,75,85),3)){g.DrawLine(p,-width*.25f,-height/2-4,-width*.25f,-height/2-14);g.DrawLine(p,width*.25f,-height/2-4,width*.25f,-height/2-14);g.DrawLine(p,-width*.25f,-height/2-14,width*.25f,-height/2-14);}g.Restore(state);}
  internal static void Draw(Graphics g,SnowBrick b,bool input,bool mould=true){if(b.Area<=0)return;var state=g.Save();g.TranslateTransform((float)b.X,(float)b.Y);g.RotateTransform((float)b.Angle);var rect=new RectangleF((float)-b.Width/2,(float)-b.Height/2,(float)b.Width,(float)b.Height);using(var shape=PanelTheme.Round(rect,3)){if(input){using(var hit=new SolidBrush(Color.FromArgb(1,255,255,255)))g.FillPath(hit,shape);}else{using(var shade=new LinearGradientBrush(rect,Color.White,Color.FromArgb(174,198,220),80))g.FillPath(shade,shape);SnowArt.GrainFill(g,shape,rect.X,rect.Y);using(var pen=new Pen(Color.FromArgb(230,249,255),1.4f))g.DrawPath(pen,shape);}}g.Restore(state);if(!input&&b.Held&&mould)DrawMould(g,(float)b.X,(float)b.Y,(float)Math.Max(30,b.Width),(float)Math.Max(14,b.Height),(float)b.Angle);}
 }
}
