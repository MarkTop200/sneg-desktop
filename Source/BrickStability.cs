using System;
using System.Linq;
using System.Collections.Generic;

namespace Sneg {
 internal sealed partial class SnowWorld {
  void UpdateBrickStability(double dt){
   // Loads propagate from the top down. A pinned block carries its own load.
   var loads=new Dictionary<SnowBrick,double[]>();
   foreach(var b in Bricks)if(!b.Pinned&&!b.Held)loads[b]=new[]{b.Area,b.Area*b.X};
   foreach(var b in Bricks.OrderBy(v=>v.Y))if(loads.ContainsKey(b)&&b.OnBrick!=null&&loads.ContainsKey(b.OnBrick)){
    loads[b.OnBrick][0]+=loads[b][0];loads[b.OnBrick][1]+=loads[b][1];
   }
   foreach(var b in Bricks.OrderByDescending(v=>v.Y).ToArray()){
    if(b.Pinned||b.Held||b.OnBrick==null||!loads.ContainsKey(b)){b.TipClock=0;continue;}
    var below=b.OnBrick;if(!Bricks.Contains(below)||below.Held){b.TipClock=0;continue;}
    double left=Math.Max(b.X-b.HalfX,below.X-below.HalfX),right=Math.Min(b.X+b.HalfX,below.X+below.HalfX);
    double center=loads[b][1]/Math.Max(1,loads[b][0]),overhang=center<left?center-left:center>right?center-right:0;
    if(Math.Abs(overhang)<=.5){b.TipClock=0;continue;}
    b.TipClock+=dt*Math.Sqrt(1+Math.Abs(overhang)/8);if(b.TipClock<.12)continue;
    double direction=Math.Sign(overhang),vx=direction*(130+Math.Min(180,Math.Abs(overhang)*3));
    DislodgeBrick(b,vx,-60);b.VX+=vx;b.VY=-35;b.Spin+=direction*(100+Math.Min(180,Math.Abs(overhang)*5));b.TipClock=0;
   }
  }
  bool HitHandfulBrick(Flake f,double oldX,double oldY,double dt,double surfaceTime){
   double best=surfaceTime,nx=0,ny=0;SnowBrick target=null;
   foreach(var brick in Bricks){if(brick.Held||brick.Area<20||brick==f.IgnoreBrick&&f.Age<f.IgnoreBrickUntil)continue;double t,x,y;
    if(SweepSnowBrick(oldX,oldY,f.Radius*.65,brick,f.X-oldX-BrickVelocityX(brick)*dt,f.Y-oldY-BrickVelocityY(brick)*dt,out t,out x,out y)&&t<best){best=t;target=brick;nx=x;ny=y;}
   }
   if(target==null)return false;
   double bx=BrickVelocityX(target),by=BrickVelocityY(target),approach=(f.VX-bx)*nx+(f.VY-by)*ny;
   if(approach<=0)return false;
   double xHit=oldX+(f.X-oldX)*best-nx*2,yHit=oldY+(f.Y-oldY)*best-ny*2;
   double vx=f.VX-1.35*approach*nx,vy=f.VY-1.35*approach*ny;
   if(f.BrickShattered&&!f.Clump){f.X=xHit;f.Y=yHit;f.VX=vx;f.VY=vy;f.IgnoreBrick=target;f.IgnoreBrickUntil=f.Age+.1;return true;}
   double mass=f.SnowArea>0?f.SnowArea:Math.PI*f.Radius*f.Radius*2;
   Flakes.Remove(f);ScatterHandfulImpact(mass,xHit,yHit,vx,vy,target,nx,ny);
   if(Sound!=null)Sound("ball",mass);return true;
  }
  void ScatterHandfulImpact(double area,double x,double y,double vx,double vy,SnowBrick target,double nx,double ny){
   int desired=Math.Min(64,Math.Max(6,(int)Math.Ceiling(area/8))),count=Math.Min(desired,Math.Max(1,EffectiveLimit-Flakes.Count));double remaining=area;
   for(int i=0;i<count;i++){
    double mass=i==count-1?remaining:area/count;remaining-=mass;double fan=(random.NextDouble()-.5)*280;
    Flakes.Add(new Flake{X=x-ny*fan*.025,Y=y+nx*fan*.025,VX=vx-ny*fan,VY=vy+nx*fan,Radius=Math.Min(2.2,Math.Max(.8,Math.Sqrt(mass/Math.PI))),SnowArea=mass,Alpha=225+random.Next(26),Debris=true,ImpactDust=true,Handful=true,BrickShattered=true,IgnoreBrick=target,IgnoreBrickUntil=.12});
   }
  }

 }
}
