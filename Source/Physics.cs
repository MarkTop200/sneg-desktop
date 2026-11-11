using System;
using System.Drawing;
using System.Collections.Generic;
using System.Linq;

namespace Sneg {
 internal sealed class Surface {
  public long Id; public uint Pid; public IntPtr Window; public double Left,Y,Width; public bool Active=true,Floor;
  public double[] Heights; public List<Rectangle> Occluders=new List<Rectangle>();
  public double PlowBank,PlowClock;
  public double MaxHeight=60;
  public double OverflowLeft,OverflowRight,SpillLeftClock,SpillRightClock,EdgeClock;
  public Surface(long id,double x,double y,double width){Id=id;Left=x;Y=y;Width=Math.Max(1,width);Heights=new double[Math.Max(1,(int)Math.Ceiling(Width/6))];}
  public double Cell {get{return Width/Heights.Length;}}
  public double Area {get{return Heights.Sum()*Cell;}}
  public double LeftWrap {get{return Floor?0:Math.Min(30,Math.Sqrt(Math.Max(0,Heights[0]))*4);}}
  public double RightWrap {get{return Floor?0:Math.Min(30,Math.Sqrt(Math.Max(0,Heights[Heights.Length-1]))*4);}}
  public bool WrapContains(double x,double y){return !Floor&&y>=Y-2&&((x>=Left-8&&x<=Left+26&&y<=Y+LeftWrap)||(x>=Left+Width-26&&x<=Left+Width+8&&y<=Y+RightWrap));}
  public int Index(double x){return Math.Max(0,Math.Min(Heights.Length-1,(int)((x-Left)/Cell)));}
  public double Height(double x){
   double at=(x-Left)/Cell-.5;int i=(int)Math.Floor(at);double t=at-i;
   double a=Heights[Math.Max(0,Math.Min(Heights.Length-1,i))],b=Heights[Math.Max(0,Math.Min(Heights.Length-1,i+1))];double h=a+(b-a)*t;
   if(!Floor){double edge=Math.Min(Math.Max(0,x-Left),Math.Max(0,Left+Width-x));double radius=Math.Min(14,Math.Max(0,h));if(edge<radius)h-=radius-Math.Sqrt(Math.Max(0,radius*radius-(radius-edge)*(radius-edge)));}
   return Math.Max(0,h);
  }
  public bool Visible(double x,double y){if(!Active||x<Left||x>=Left+Width)return false;foreach(var r in Occluders)if(x>=r.Left&&x<r.Right&&y>=r.Top&&y<r.Bottom)return false;return true;}
  public double Add(double x,double area){
   if(area<=0)return 0;int center=Index(x);double remaining=area;
   // Fill nearby space before sending excess to the window edges.
   for(int radius=0;radius<Heights.Length&&remaining>.00001;radius++){
    int a=center-radius,b=center+radius;
    if(a>=0){double put=Math.Min(remaining,Math.Max(0,MaxHeight-Heights[a])*Cell);Heights[a]+=put/Cell;remaining-=put;}
    if(radius>0&&b<Heights.Length){double put=Math.Min(remaining,Math.Max(0,MaxHeight-Heights[b])*Cell);Heights[b]+=put/Cell;remaining-=put;}
   }
   if(remaining>0&&!Floor){double right=Math.Max(0,Math.Min(1,(x-Left)/Width));OverflowLeft+=remaining*(1-right);OverflowRight+=remaining*right;}
   return area-remaining;
  }
  public double Take(double x,double radius,double budget){double total=0;int a=Index(x-radius),b=Index(x+radius);for(int i=a;i<=b&&budget>0;i++){double q=Math.Min(Heights[i]*Cell,budget);Heights[i]-=q/Cell;budget-=q;total+=q;}return total;}
  public double Resize(double width){width=Math.Max(1,width);var old=Heights;double oldCell=Cell,oldWidth=Width;Width=width;Heights=new double[Math.Max(1,(int)Math.Ceiling(width/6))];double retained=0;
   // Integrate overlapping bins: conserve area retained inside the new extent.
   for(int j=0;j<Heights.Length;j++){double l=j*Cell,r=Math.Min(width,(j+1)*Cell),area=0;int a=(int)(l/oldCell),b=Math.Min(old.Length-1,(int)(r/oldCell));for(int i=a;i<=b;i++)area+=Math.Max(0,Math.Min(r,(i+1)*oldCell)-Math.Max(l,i*oldCell))*old[i];Heights[j]=area/Cell;retained+=area;}
   return Math.Max(0,old.Sum()*oldCell-retained);
  }
  public void Relax(){for(int i=0;i<Heights.Length-1;i++){double delta=Heights[i]-Heights[i+1];if(Math.Abs(delta)>Cell*0.65){double q=(Math.Abs(delta)-Cell*0.65)*0.2;if(delta>0){Heights[i]-=q;Heights[i+1]+=q;}else{Heights[i]+=q;Heights[i+1]-=q;}}}}
 }
 internal sealed class Flake {public double X,Y,VX,VY,Radius,Phase,Age,SnowArea,Spin,SpinVelocity,IgnoreUntil;public long IgnoreSurface;public int Alpha;public bool Debris,GroundOnly,Clump,PlowSpray;}
 internal sealed class Ball {
  public double X,Y,VX,VY,Area,Age; public Surface Support;public Ball OnBall;public bool Held,Face,Loose,ThrowArmed;public Surface CarrySource;
  public double Radius {get{return Math.Sqrt(Math.Max(0,Area)/Math.PI);}}
  public bool Visible {get{return Support==null||Support.Active;}}
 }
 internal enum Tool {None,Snowball,Mitten,Shovel,Handful}
 internal sealed class SnowWorld {
  public readonly List<Surface> Surfaces=new List<Surface>();public readonly List<Flake> Flakes=new List<Flake>();public readonly List<Ball> Balls=new List<Ball>();
  public List<Rectangle> Monitors=new List<Rectangle>();public readonly Settings Settings;
  public Action<string,double> Sound;
  public bool Emitting=true,Paused;public Tool Tool;public Ball Held;public double Time,PlowX;public bool Plowing;public int PlowDirection=1;public int EffectiveLimit;
  public PointF Pointer;public bool ShowTool,ToolDown;
  public const double Step=1.0/120;double accumulator,spawn,plowTime,wind,speed=100;Random random;PointF previousPointer;double prevPointerTime,lastMoveTime,lastThrowVX,lastThrowVY;readonly Queue<PointerSample> samples=new Queue<PointerSample>();
  struct PointerSample{public PointF P;public double T;public PointerSample(PointF p,double t){P=p;T=t;}}
  public SnowWorld(Settings settings,int seed){Settings=settings;random=new Random(seed);EffectiveLimit=settings.MaxParticles;}
  public void Advance(double elapsed){if(Paused||elapsed>0.25||elapsed<0){accumulator=0;return;}accumulator+=elapsed;int n=0;while(accumulator+1e-10>=Step&&n++<8){Update(Step);accumulator-=Step;}if(n>8)accumulator=0;}
  public void ResetClock(){accumulator=0;}
  public void Clear(){CancelDrag();Flakes.Clear();Balls.Clear();foreach(var s in Surfaces){Array.Clear(s.Heights,0,s.Heights.Length);s.PlowBank=s.PlowClock=0;s.OverflowLeft=s.OverflowRight=s.SpillLeftClock=s.SpillRightClock=s.EdgeClock=0;}Plowing=false;spawn=0;}
  public void CancelDrag(){ToolDown=false;if(Held!=null){if(Held.Loose){Balls.Remove(Held);Scatter(Held.Area,Held.X,Held.Y,0,20,Held.CarrySource);Held=null;samples.Clear();return;}Held.Held=false;Held.VX=0;Held.VY=0;if(Held.Radius<4)Balls.Remove(Held);}Held=null;samples.Clear();}
  public void Sync(List<WindowInfo> windows,List<Rectangle> workAreas){var seen=new HashSet<long>();var blockers=new List<Rectangle>();
   foreach(var w in windows){long id=w.Handle.ToInt64();if(w.Surface){var s=Surfaces.Find(v=>v.Id==id);if(s!=null&&s.Pid!=w.Pid){RemoveSurface(s);s=null;}if(s==null){s=new Surface(id,w.Bounds.Left,w.Bounds.Top,w.Bounds.Width){Window=w.Handle,Pid=w.Pid};Surfaces.Add(s);}
    double dx=w.Bounds.Left-s.Left,dy=w.Bounds.Top-s.Y;
    if(Math.Abs(dx)>.5||Math.Abs(dy)>.5)Shed(s,dx,dy);
    double oldWidth=s.Width;if(Math.Abs(oldWidth-w.Bounds.Width)>.1){double lost=s.Resize(w.Bounds.Width);if(lost>0)Debris(s.Left+w.Bounds.Width,s.Y,Math.Min(20,(int)(lost/20)+1));}
    s.Left=w.Bounds.Left;s.Y=w.Bounds.Top;s.Active=true;s.Occluders=new List<Rectangle>(blockers);seen.Add(id);
    foreach(var b in Balls)if(b.Support==s&&!b.Held){b.X+=dx;b.Y+=dy;if(b.X-b.Radius>s.Left+s.Width){b.Support=null;b.OnBall=null;}}
   }blockers.Add(w.Bounds);}
   for(int i=Surfaces.Count-1;i>=0;i--){var s=Surfaces[i];if(s.Floor)continue;if(!seen.Contains(s.Id)){if(Native.IsWindow(s.Window))s.Active=false;else RemoveSurface(s);}}
   // The physical screen edge stays visible even above a taskbar or a maximized window.
   var ground=Monitors.Count>0?Monitors:workAreas;
   for(int k=0;k<ground.Count;k++){var r=ground[k];long id=-1-k;seen.Add(id);var s=Surfaces.Find(v=>v.Id==id);if(s==null){s=new Surface(id,r.Left,r.Bottom,r.Width){Floor=true};Surfaces.Add(s);}if(Math.Abs(s.Width-r.Width)>.1)s.Resize(r.Width);double dx=r.Left-s.Left,dy=r.Bottom-s.Y;s.Left=r.Left;s.Y=r.Bottom;SetGroundLimit(s,r.Height);s.Active=true;s.Occluders.Clear();foreach(var b in Balls)if(b.Support==s&&!b.Held){b.X+=dx;b.Y+=dy;}}
   foreach(var s in Surfaces.Where(v=>v.Floor&&!seen.Contains(v.Id)).ToArray())RemoveSurface(s);
  }
  internal void Shed(Surface s,double dx,double dy){
   double area=s.Area+s.OverflowLeft+s.OverflowRight;
   if(area>.01){
    // Wider/deeper deposits break into larger heaps, rather than a fixed cloud of dots.
    double mean=area/Math.Max(1,s.Width);int count=Math.Max(1,(int)Math.Ceiling(s.Width/(34+mean)));
    count=Math.Min(Math.Min(32,s.Heights.Length),Math.Min(count,Math.Max(1,EffectiveLimit/2)));
    // Reserve room for falling chunks by replacing decorative airborne flakes first.
    for(int i=Flakes.Count-1;i>=0&&Flakes.Count+count>EffectiveLimit;i--)if(!Flakes[i].Debris)Flakes.RemoveAt(i);
    count=Math.Min(count,Math.Max(0,EffectiveLimit-Flakes.Count));
    if(count>0)for(int group=0;group<count;group++){
     int from=group*s.Heights.Length/count,to=(group+1)*s.Heights.Length/count;double mass=0,x=0,y=0;
     for(int i=from;i<to;i++){double q=s.Heights[i]*s.Cell;mass+=q;x+=(s.Left+(i+.5)*s.Cell)*q;y+=(s.Y-s.Heights[i]*.5)*q;}
     if(group==0){mass+=s.OverflowLeft;x+=(s.Left-2)*s.OverflowLeft;y+=(s.Y-40)*s.OverflowLeft;}
     if(group==count-1){mass+=s.OverflowRight;x+=(s.Left+s.Width+2)*s.OverflowRight;y+=(s.Y-40)*s.OverflowRight;}
     if(mass>.01)Flakes.Add(CreateClump(mass,x/mass,y/mass,s.Id,Math.Max(-160,Math.Min(160,dx*2))+(random.NextDouble()-.5)*90,25+random.NextDouble()*90+Math.Max(0,dy)*2));
    }
    Array.Clear(s.Heights,0,s.Heights.Length);s.OverflowLeft=s.OverflowRight=s.SpillLeftClock=s.SpillRightClock=s.EdgeClock=0;
   }
   foreach(var ball in Balls)if(ball.Support==s&&!ball.Held){ball.Support=null;ball.OnBall=null;ball.Face=false;ball.VX=Math.Max(-100,Math.Min(100,dx));ball.VY=Math.Max(35,Math.Min(200,dy*2));}
  }
  void RemoveSurface(Surface s){Shed(s,0,0);Surfaces.Remove(s);}
  internal static double ClumpRadius(double area){return Math.Max(.5,Math.Min(96,Math.Sqrt(Math.Max(0,area)/Math.PI)));}
  Flake CreateClump(double amount,double x,double y,long source,double vx,double vy){return new Flake{X=x,Y=y,VX=vx,VY=vy,Radius=ClumpRadius(amount),SnowArea=amount,IgnoreSurface=source,Phase=random.NextDouble()*6.28,Spin=random.NextDouble()*360,SpinVelocity=(random.NextDouble()-.5)*150,Alpha=250,Debris=true,Clump=true};}
  void SetGroundLimit(Surface s,int height){s.MaxHeight=Settings.FillDesktop?Math.Max(1,height):60;for(int i=0;i<s.Heights.Length;i++)s.Heights[i]=Math.Min(s.Heights[i],s.MaxHeight);}
  public void RefreshGroundLimits(){foreach(var s in Surfaces)if(s.Floor){var r=Monitors.FirstOrDefault(m=>m.Left==s.Left&&m.Bottom==s.Y);if(r.Width>0)SetGroundLimit(s,r.Height);}}
  public void Seed(){foreach(var s in Surfaces)if(s.Active)for(int i=0;i<s.Heights.Length;i++)s.Heights[i]=14+3*Math.Sin(i*.23);}
  public void FillWindows(){foreach(var s in Surfaces)if(s.Active&&!s.Floor)for(int i=0;i<s.Heights.Length;i++)s.Heights[i]=60;}
  void EdgeOverflow(Surface s,double dt){
   if(!s.Active||s.Floor)return;s.EdgeClock+=dt;
   if(s.EdgeClock>=.12){s.EdgeClock=0;if(s.Area/s.Width>=54){if(s.Heights[0]>=52)s.OverflowLeft+=s.Take(s.Left,12,85);if(s.Heights[s.Heights.Length-1]>=52)s.OverflowRight+=s.Take(s.Left+s.Width,12,85);}}
   if(s.OverflowLeft>.01){s.SpillLeftClock+=dt;if(s.SpillLeftClock>=.08&&(s.OverflowLeft>=25||s.SpillLeftClock>=.3)&&EmitEdge(s,true))s.SpillLeftClock=0;}
   if(s.OverflowRight>.01){s.SpillRightClock+=dt;if(s.SpillRightClock>=.08&&(s.OverflowRight>=25||s.SpillRightClock>=.3)&&EmitEdge(s,false))s.SpillRightClock=0;}
  }
  bool EmitEdge(Surface s,bool left){
   if(Flakes.Count>=EffectiveLimit){int spare=Flakes.FindLastIndex(f=>!f.Debris);if(spare<0)return false;Flakes.RemoveAt(spare);}
   double amount=Math.Min(200,left?s.OverflowLeft:s.OverflowRight);if(amount<=0)return false;
   double edge=left?s.Left:s.Left+s.Width;double dir=left?-1:1;
   Flakes.Add(CreateClump(amount,edge+dir*(6+ClumpRadius(amount)*.5),s.Y-s.Height(edge)-3,s.Id,dir*(22+random.NextDouble()*42),35+random.NextDouble()*65));
   if(left)s.OverflowLeft-=amount;else s.OverflowRight-=amount;return true;
  }
  void Debris(double x,double y,int count){for(int i=0;i<count&&Flakes.Count<EffectiveLimit;i++)Flakes.Add(new Flake{X=x+(random.NextDouble()-.5)*45,Y=y-4,VY=45+random.NextDouble()*75,Radius=1+random.NextDouble()*2,Phase=random.NextDouble()*6,Alpha=190,Debris=true});}
  void Spawn(){if(Monitors.Count==0)return;int total=Monitors.Sum(r=>r.Width),pick=random.Next(total);Rectangle m=Monitors[0];foreach(var r in Monitors){if(pick<r.Width){m=r;break;}pick-=r.Width;}
   // A separate share of the snowfall travels in front of windows to feed the screen-edge drift.
   bool ground=random.NextDouble()<.32;
   double spawnY=ground?m.Top+m.Height*.60+random.NextDouble()*m.Height*.15:m.Top-8;
   if(ground){var floor=Surfaces.Find(v=>v.Floor&&v.Left==m.Left&&v.Y==m.Bottom);if(floor!=null)spawnY=Math.Min(spawnY,floor.Y-floor.Heights.Max()-8);}

   Flakes.Add(new Flake{X=m.Left+pick,Y=spawnY,VY=45+random.NextDouble()*75,Radius=(1+random.NextDouble()*2.3)*Settings.Size/100.0,Phase=random.NextDouble()*6.28,Alpha=110+random.Next(140),GroundOnly=ground});}
  void Update(double dt){Time+=dt;double smooth=1-Math.Exp(-3*dt);wind+=(Settings.Wind-wind)*smooth;speed+=(Settings.Speed-speed)*smooth;if(Emitting){spawn+=Settings.Intensity*dt;while(spawn>=1){spawn--;if(Flakes.Count<EffectiveLimit)Spawn();}}
   double bottom=Monitors.Count==0?2000:Monitors.Max(r=>r.Bottom)+150;
   for(int i=Flakes.Count-1;i>=0;i--){var f=Flakes[i];double ox=f.X,oy=f.Y;f.Age+=dt;if(f.Debris){f.VY=Math.Min(1300,f.VY+620*dt);f.VX*=Math.Exp(-.4*dt);}f.Spin+=f.SpinVelocity*dt;f.X+=(wind*(f.Debris ? .25 : 1)+f.VX+Math.Sin(Time*1.5+f.Phase)*(f.Debris?5:16))*dt;f.Y+=f.VY*(f.Debris?1:speed/100)*dt;Surface best=null;double bestT=2;
    foreach(var s in Surfaces)if(!f.PlowSpray&&s.Active&&(!f.GroundOnly||s.Floor)&&(f.IgnoreSurface==0||s.Id!=f.IgnoreSurface||f.IgnoreUntil>0&&f.Age>=f.IgnoreUntil)){double level=s.Y-s.Height(f.X)-(f.Clump?f.Radius*.65:0);if(f.Y>=level&&(oy<=level||f.IgnoreUntil>0&&f.VY>0&&oy<=s.Y)){double t=(level-oy)/Math.Max(.0001,f.Y-oy),x=ox+(f.X-ox)*t;if(s.Visible(x,level+(f.Clump?f.Radius*.65:0)-1)&&t<bestT){best=s;bestT=t;}}}
    if(best!=null){if(Settings.Accumulate)best.Add(ox+(f.X-ox)*bestT,f.SnowArea>0?f.SnowArea:Math.PI*f.Radius*f.Radius*2);Flakes.RemoveAt(i);}else if(f.Y>bottom||f.Age>(f.PlowSpray?1.25:50))Flakes.RemoveAt(i);
   }
   foreach(var s in Surfaces)if(s.Active){s.Relax();EdgeOverflow(s,dt);}
   // Bottom balls first: each upper sphere resolves against its current support.
   foreach(var b in Balls.OrderByDescending(v=>v.Y).ToArray()){
    b.Age+=dt;if(!Balls.Contains(b)||b.Held||!b.Visible)continue;double r=b.Radius;
    if(b.Loose){double oldY=b.Y;b.VY=Math.Min(1300,b.VY+620*dt);b.X+=b.VX*dt;b.Y+=b.VY*dt;Surface caught=null;if(b.Age>.16&&b.VY>0)foreach(var surface in Surfaces)if(surface.Active&&b.X>=surface.Left&&b.X<surface.Left+surface.Width){double level=surface.Y-surface.Height(b.X);if(oldY<=surface.Y&&b.Y+r>=level&&surface.Visible(b.X,level-1)&&(caught==null||level<caught.Y-caught.Height(b.X)))caught=surface;}if(caught!=null){if(Settings.Accumulate)caught.Add(b.X,b.Area);Balls.Remove(b);}else if(Flakes.Count<EffectiveLimit){Scatter(b.Area,b.X,b.Y,b.VX,b.VY,b.CarrySource,true);Balls.Remove(b);}continue;}
    if(b.OnBall!=null&&(!Balls.Contains(b.OnBall)||b.OnBall.Held||Math.Abs(b.X-b.OnBall.X)>r+b.OnBall.Radius-2)){b.OnBall=null;b.Support=null;b.Face=false;}
    if(b.OnBall!=null){b.Support=b.OnBall.Support;b.X=b.OnBall.X;b.Y=b.OnBall.Y-b.OnBall.Radius-r+1;b.VX=b.VY=0;continue;}
    if(b.Support!=null){var s=b.Support;if(b.X>=s.Left&&b.X<s.Left+s.Width){b.Y=s.Y-s.Height(b.X)-r;b.VY=0;b.VX*=Math.Exp(-2.5*dt);b.X+=b.VX*dt;continue;}b.Support=null;b.Face=false;}
    double ox=b.X,oy=b.Y;b.VY=Math.Min(3000,b.VY+550*dt);double nx=ox+b.VX*dt,ny=oy+b.VY*dt;Surface landing=null;Ball sphere=null;double hit=2,levelHit=0;
    foreach(var s in Surfaces)if(s.Active){double level=s.Y-s.Height(nx)-r;if(oy<=level+.1&&ny>=level&&ny>oy){double t=Math.Max(0,(level-oy)/(ny-oy)),x=ox+(nx-ox)*t;if(s.Visible(x,level+r-1)&&t<hit){hit=t;landing=s;sphere=null;levelHit=level;}}}
    foreach(var other in Balls)if(other!=b&&!other.Held&&other.Visible&&other.Radius>=r*.7&&other.Y>oy){double t; if(SweepCircle(ox,oy,nx,ny,other.X,other.Y,r+other.Radius,out t)&&t<hit){double x=ox+(nx-ox)*t;if(Math.Abs(x-other.X)<(r+other.Radius)*.65){hit=t;landing=other.Support;sphere=other;levelHit=other.Y-other.Radius-r+1;}}}
    if(hit<=1&&b.ThrowArmed){double impact=b.VY;if(sphere!=null){double cx=ox+(nx-ox)*hit-sphere.X,cy=oy+(ny-oy)*hit-sphere.Y,len=Math.Sqrt(cx*cx+cy*cy);if(len>0)impact=-((b.VX-sphere.VX)*cx+(b.VY-sphere.VY)*cy)/len;}if(impact>=500){double x=ox+(nx-ox)*hit,y=oy+(ny-oy)*hit;Scatter(b.Area,x,y,b.VX*.18,-Math.Min(240,impact*.2),landing);Balls.Remove(b);foreach(var upper in Balls)if(upper.OnBall==b){upper.OnBall=null;upper.Support=null;upper.Face=false;}if(Sound!=null)Sound("ball",b.Area);continue;}}
    if(hit<=1){b.ThrowArmed=false;b.X=sphere==null?ox+(nx-ox)*hit:sphere.X;b.Y=levelHit;b.Support=landing;b.OnBall=sphere;b.VY=0;b.VX*=.65;if(sphere!=null){b.VX=0;RecognizeSnowman(b);}}
    else{b.X=nx;b.Y=ny;}
   }
   // Resolve horizontal contacts between rolling balls on the same ledge.
   for(int i=0;i<Balls.Count;i++)for(int j=i+1;j<Balls.Count;j++){var a=Balls[i];var b=Balls[j];if(a.Held||b.Held||a.Support==null||a.Support!=b.Support||a.OnBall!=null||b.OnBall!=null)continue;double dy=a.Y-b.Y,sum=a.Radius+b.Radius;if(Math.Abs(dy)>=sum)continue;double minimum=Math.Sqrt(sum*sum-dy*dy),dx=b.X-a.X;if(Math.Abs(dx)<minimum){double sign=dx>=0?1:-1,shift=(minimum-Math.Abs(dx))*.5;a.X-=sign*shift;b.X+=sign*shift;double v=a.VX;a.VX=b.VX*.65;b.VX=v*.65;}}
   Balls.RemoveAll(b=>!b.Held&&(b.Y>bottom+300||b.Age>600&&b.Support==null&&b.OnBall==null));
   if(Plowing){plowTime+=dt;double depth=0;foreach(var s in Surfaces)if(s.Floor&&PlowX+PlowDirection*48>=s.Left&&PlowX+PlowDirection*48<s.Left+s.Width)depth=Math.Max(depth,s.Height(PlowX+PlowDirection*48));PlowX+=PlowDirection*180/(1+Math.Min(2.5,depth/100))*dt;
    foreach(var s in Surfaces)if(s.Floor&&PlowX+PlowDirection*48>=s.Left-32&&PlowX+PlowDirection*48<s.Left+s.Width+32)s.PlowBank+=s.Take(PlowX+PlowDirection*48,32,double.MaxValue);
    if(Monitors.Count==0||(PlowDirection>0?PlowX>Monitors.Max(r=>r.Right)+80:PlowX<Monitors.Min(r=>r.Left)-80))Plowing=false;}
   foreach(var s in Surfaces)if(s.Floor&&s.PlowBank>0){s.PlowClock+=dt;if(s.PlowClock>=.04&&Flakes.Count<EffectiveLimit){s.PlowClock=0;double amount=Math.Min(s.PlowBank,Math.Max(70,s.PlowBank*.2));s.PlowBank-=amount;var f=CreateClump(amount,PlowX+PlowDirection*42,s.Y-24,0,PlowDirection*(-90-random.NextDouble()*110),-210-random.NextDouble()*160);f.Radius=Math.Min(22,f.Radius);f.PlowSpray=true;Flakes.Add(f);}}

  }
  internal void Scatter(double area,double x,double y,double vx,double vy,Surface fallback,bool directional=false){if(area<=0)return;int count=Math.Min(32,Math.Min(Math.Max(8,(int)(Math.Sqrt(area)/2)),Math.Max(0,EffectiveLimit-Flakes.Count)));if(count==0){if(fallback==null)fallback=Surfaces.FirstOrDefault(v=>v.Active&&v.Floor&&x>=v.Left&&x<v.Left+v.Width);if(fallback!=null&&Surfaces.Contains(fallback))fallback.Add(x,area);return;}double remaining=area;for(int i=0;i<count;i++){double amount=i==count-1?remaining:area/count;remaining-=amount;double spread=directional?Math.Min(65,Math.Max(8,Math.Abs(vx)*.35)):260;var f=CreateClump(amount,x+(random.NextDouble()-.5)*12,y-4-random.NextDouble()*8,0,vx+(random.NextDouble()-.5)*spread,vy-random.NextDouble()*(directional?45:170));if(directional&&fallback!=null){f.IgnoreSurface=fallback.Id;f.IgnoreUntil=.16;}Flakes.Add(f);}}
  internal static bool SweepCircle(double x,double y,double nx,double ny,double cx,double cy,double r,out double t){double dx=nx-x,dy=ny-y,fx=x-cx,fy=y-cy,a=dx*dx+dy*dy,b=2*(fx*dx+fy*dy),c=fx*fx+fy*fy-r*r;t=0;if(c<=0)return true;if(a<1e-12)return false;double d=b*b-4*a*c;if(d<0)return false;t=(-b-Math.Sqrt(d))/(2*a);return t>=0&&t<=1;}
  void RecognizeSnowman(Ball top){var mid=top.OnBall;var bot=mid==null?null:mid.OnBall;if(bot!=null&&bot.Support!=null&&top.Radius<=mid.Radius*1.15&&mid.Radius<=bot.Radius*1.15)top.Face=true;}
  public void StartPlow(){Plowing=true;plowTime=0;PlowDirection=random.Next(2)==0?1:-1;PlowX=Monitors.Count==0?0:PlowDirection>0?Monitors.Min(r=>r.Left)-50:Monitors.Max(r=>r.Right)+50;}
  public Ball PickBall(PointF p){return Balls.LastOrDefault(b=>b.Visible&&Math.Pow(p.X-b.X,2)+Math.Pow(p.Y-b.Y,2)<=b.Radius*b.Radius&&(b.Support==null||b.Support.Visible(p.X,p.Y)));}
  public Surface PickSnow(PointF p){return Surfaces.FirstOrDefault(s=>s.Active&&(((p.X>=s.Left&&p.X<s.Left+s.Width&&p.Y>=s.Y-s.Height(p.X)-2&&p.Y<=s.Y+1)||s.WrapContains(p.X,p.Y)))&&s.Heights[s.Index(p.X)]>.5&&s.Visible(Math.Max(s.Left,Math.Min(s.Left+s.Width-.01,p.X)),Math.Min(s.Y-1,p.Y)));}
  public void BeginDrag(PointF p){if(Tool==Tool.None||Paused)return;ToolDown=true;lastThrowVX=lastThrowVY=0;lastMoveTime=-100;previousPointer=p;prevPointerTime=Time;samples.Clear();samples.Enqueue(new PointerSample(p,Time));if(Tool==Tool.Handful){var source=PickSnow(p);if(source==null||Balls.Count>=60)return;Held=new Ball{X=p.X,Y=p.Y,Held=true,Loose=true,CarrySource=source};Balls.Add(Held);}if(Tool==Tool.Snowball){Held=PickBall(p);if(Held==null){if(PickSnow(p)==null||Balls.Count>=60)return;Held=new Ball{X=p.X,Y=p.Y,Held=true};Balls.Add(Held);}Held.Held=true;Held.ThrowArmed=false;Held.Support=null;Held.OnBall=null;Held.Face=false;foreach(var b in Balls)if(b.OnBall==Held){b.OnBall=null;b.Support=null;b.Face=false;}}Drag(p);}
  public void Drag(PointF p){if(Tool==Tool.None||Paused)return;double dx=p.X-previousPointer.X,dy=p.Y-previousPointer.Y;if(dx*dx+dy*dy>.0025){double movementTime=Math.Max(.008,Time-prevPointerTime);lastThrowVX=Math.Max(-1800,Math.Min(1800,dx/movementTime));lastThrowVY=Math.Max(-1800,Math.Min(1800,dy/movementTime));lastMoveTime=Time;}int n=Math.Max(1,(int)Math.Ceiling(Math.Sqrt(dx*dx+dy*dy)/5));n=Math.Min(n,3000);
   for(int j=1;j<=n;j++){double x=previousPointer.X+dx*j/n,y=previousPointer.Y+dy*j/n;foreach(var s in Surfaces){bool wrap=s.WrapContains(x,y);double sx=Math.Max(s.Left,Math.Min(s.Left+s.Width-.01,x));if(!s.Active||(!wrap&&(x<s.Left||x>=s.Left+s.Width))||!s.Visible(sx,Math.Min(y,s.Y-1)))continue;if(!wrap&&Math.Abs(y-(s.Y-s.Height(x)))>48)continue;
     if((Tool==Tool.Snowball||Tool==Tool.Handful)&&Held!=null){double cap=Tool==Tool.Handful?22:52;double q=s.Take(x,Tool==Tool.Handful?18:20,Math.Max(0,Math.PI*cap*cap-Held.Area));Held.Area+=q;if(q>0&&Sound!=null)Sound("ball",q);}else if(Tool==Tool.Mitten||Tool==Tool.Shovel){double q=s.Take(x,Tool==Tool.Mitten?24:65,double.MaxValue);if(q>0){if(Sound!=null)Sound(Tool==Tool.Mitten?"mitten":"shovel",q);if(Tool==Tool.Mitten)Debris(x,y,2);}}
   }}if(Held!=null){Held.X=p.X;Held.Y=p.Y;}previousPointer=p;prevPointerTime=Time;samples.Enqueue(new PointerSample(p,Time));while(samples.Count>1&&Time-samples.Peek().T>.11)samples.Dequeue();
  }
  public void EndDrag(PointF p){Drag(p);ToolDown=false;if(Held!=null){var first=samples.Count>0?samples.Peek():new PointerSample(p,Time);double dt=Time-first.T;Held.Held=false;if(Held.Radius>=4&&Sound!=null)Sound("ball",Held.Area);if(dt>.008){Held.VX=Math.Max(-1800,Math.Min(1800,(p.X-first.P.X)/dt));Held.VY=Math.Max(-1800,Math.Min(1800,(p.Y-first.P.Y)/dt));}
    if(Held.Loose){var carried=Held;Held=null;if(Time-lastMoveTime<=.18){carried.VX=lastThrowVX;carried.VY=lastThrowVY;double length=Math.Sqrt(carried.VX*carried.VX+carried.VY*carried.VY);if(length>0&&length<120){carried.VX*=120/length;carried.VY*=120/length;}}else carried.VX=carried.VY=0;carried.X=p.X;carried.Y=p.Y;var source=carried.CarrySource;if(source!=null&&p.X>=source.Left&&p.X<source.Left+source.Width){double clearance=source.Y-source.Height(p.X)-carried.Radius-12;if(carried.Y>=clearance){carried.Y=clearance;carried.VY=Math.Min(-110,carried.VY);}}if(Flakes.Count<EffectiveLimit){Balls.Remove(carried);Scatter(carried.Area,carried.X,carried.Y,carried.VX,carried.VY,source,true);}samples.Clear();return;}Held.ThrowArmed=Math.Sqrt(Held.VX*Held.VX+Held.VY*Held.VY)>=650;
    // A sphere collected at the edge starts partly inside the support plane.
    // Lift it onto that plane before the swept integrator takes over.
    foreach(var s in Surfaces)if(s.Active&&Held.X>=s.Left&&Held.X<s.Left+s.Width){double level=s.Y-s.Height(Held.X);if(s.Visible(Held.X,level-1)&&Held.Y<=level+2&&Held.Y+Held.Radius>=level){Held.Y=level-Held.Radius;if(Held.VY>=-80&&!Held.ThrowArmed){Held.Support=s;Held.VY=0;}break;}}
    if(Held.Radius<4)Balls.Remove(Held);Held=null;}samples.Clear();}
 }
}
