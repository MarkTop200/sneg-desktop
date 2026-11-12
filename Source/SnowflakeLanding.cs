using System;
namespace Sneg {
 internal sealed partial class Surface {
  public double DepositSnowflake(double x,double area,double flakeRadius){if(area<=0||Floor&&SkyBlocked(x))return 0;double spread=Math.Max(5,flakeRadius*4.5),sum=0;int from=Index(x-spread),to=Index(x+spread);
   for(int i=from;i<=to;i++){double distance=Math.Abs(Left+(i+.5)*Cell-x)/spread;if(distance<1&&(!Floor||!SkyBlocked(Left+(i+.5)*Cell)))sum+=(1+Math.Cos(distance*Math.PI))*.5;}if(sum<=0)return 0;
   for(int i=from;i<=to;i++){double distance=Math.Abs(Left+(i+.5)*Cell-x)/spread;if(distance>=1||Floor&&SkyBlocked(Left+(i+.5)*Cell))continue;double amount=area*((1+Math.Cos(distance*Math.PI))*.5)/sum;if(Floor){double put=Math.Min(amount,Math.Max(0,MaxHeight-Heights[i])*Cell);Heights[i]+=put/Cell;Pending[i]+=amount-put;}else Add(Left+(i+.5)*Cell,amount);}return area;
  }
 }
 internal sealed partial class SnowWorld {
  void LandSnowflake(Surface surface,Flake flake,double x,double mass){int desired=Math.Min(8,Math.Max(2,(int)Math.Ceiling(flake.Radius*.9))),slots=Math.Max(0,EffectiveLimit-Flakes.Count+1);double fraction=Math.Min(.28,Math.Max(0,(flake.Radius-1.5)*.045));int count=Math.Min(desired,slots);if(flake.Radius<3||mass<24||count<2)fraction=0;double airborne=mass*fraction;surface.DepositSnowflake(x,mass-airborne,flake.Radius);if(airborne<=0)return;
   for(int k=0;k<count;k++){double amount=airborne/count,px=Math.Max(surface.Left+.01,Math.Min(surface.Left+surface.Width-.01,x+(random.NextDouble()-.5)*flake.Radius*1.4));if(surface.Floor&&surface.SkyBlocked(px))px=x;double vx=(random.NextDouble()-.5)*(20+flake.Radius*9)+flake.VX*.1,vy=-(15+flake.Radius*6)*(.7+random.NextDouble()*.5);var dust=CreateClump(amount,px,surface.Y-surface.Height(px)-ClumpRadius(amount)-4,surface.Id,vx,vy);dust.ImpactDust=true;dust.IgnoreUntil=.04;dust.Debris=!surface.Floor;Flakes.Add(dust);}
  }
 }
}

