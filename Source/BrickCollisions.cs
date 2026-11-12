using System;
using System.Collections.Generic;
using System.Linq;

namespace Sneg {
 internal sealed partial class SnowWorld {
  static double BrickVelocityX(SnowBrick b){return b.Pinned||b.Held||b.OnBrick!=null?0:b.VX;}
  static double BrickVelocityY(SnowBrick b){return b.Pinned||b.Held||b.Support!=null||b.OnBrick!=null?0:b.VY;}
  static double ProjectBrick(SnowBrick b,double x,double y){double a=b.Angle*Math.PI/180,c=Math.Cos(a),s=Math.Sin(a);return Math.Abs(x*c+y*s)*b.Width/2+Math.Abs(-x*s+y*c)*b.Height/2;}
  // Swept separating axes: fast projectiles cannot skip thin or rotated bricks.
  static bool SweepBrick(SnowBrick a,SnowBrick b,double dx,double dy,out double time,out double normalX,out double normalY){
   time=0;normalX=normalY=0;double exit=1,minOverlap=double.MaxValue,overlapX=0,overlapY=0;
   double[] angles={a.Angle*Math.PI/180,b.Angle*Math.PI/180};
   foreach(double angle in angles)for(int i=0;i<2;i++){
    double x=i==0?Math.Cos(angle):-Math.Sin(angle),y=i==0?Math.Sin(angle):Math.Cos(angle);
    double d=(b.X-a.X)*x+(b.Y-a.Y)*y,r=ProjectBrick(a,x,y)+ProjectBrick(b,x,y),v=dx*x+dy*y;
    double overlap=r-Math.Abs(d);if(overlap<minOverlap){minOverlap=overlap;overlapX=x*(d>=0?1:-1);overlapY=y*(d>=0?1:-1);}
    if(Math.Abs(v)<1e-9){if(Math.Abs(d)>r)return false;continue;}
    double enter=(d-r)/v,leave=(d+r)/v;if(enter>leave){double swap=enter;enter=leave;leave=swap;}
    if(enter>time){time=enter;double sign=d-v*enter>=0?1:-1;normalX=x*sign;normalY=y*sign;}
    exit=Math.Min(exit,leave);if(time>exit)return false;
   }
   if(exit<0||time>1)return false;if(normalX==0&&normalY==0){normalX=overlapX;normalY=overlapY;}return true;
  }
  void DislodgeBrick(SnowBrick b,double vx,double vy){
   if(b.Pinned||b.Held)return;b.Support=null;b.OnBrick=null;
   var changed=new HashSet<SnowBrick>{b};
   for(int i=0;i<Bricks.Count;i++){bool found=false;foreach(var upper in Bricks)if(!upper.Pinned&&!upper.Held&&upper.OnBrick!=null&&changed.Contains(upper.OnBrick)){
    upper.OnBrick=null;upper.Support=null;upper.VX+=vx*.45;upper.VY+=Math.Min(-30,vy*.4);upper.Spin+=Math.Max(-160,Math.Min(160,vx*.3));changed.Add(upper);found=true;
   }if(!found)break;}
  }
  void CollideBricks(double dt){
   for(int i=0;i<Bricks.Count;i++)for(int j=i+1;j<Bricks.Count;j++){
    var a=Bricks[i];var b=Bricks[j];if(a.Held||b.Held||a.Area<20||b.Area<20||a.Pinned&&b.Pinned)continue;
    double ax=BrickVelocityX(a),ay=BrickVelocityY(a),bx=BrickVelocityX(b),by=BrickVelocityY(b),t,x,y;
    if((ax-bx)*(ax-bx)+(ay-by)*(ay-by)<10000)continue;
    if(!SweepBrick(a,b,(ax-bx)*dt,(ay-by)*dt,out t,out x,out y))continue;
    double speed=(ax-bx)*x+(ay-by)*y;if(speed<100)continue;
    double ia=a.Pinned?0:1/a.Area,ib=b.Pinned?0:1/b.Area,jolt=1.28*speed/(ia+ib);
    if(!a.Pinned){a.X+=ax*dt*t;a.Y+=ay*dt*t;DislodgeBrick(a,-x*speed,-y*speed);a.VX=ax-jolt*ia*x;a.VY=ay-jolt*ia*y;a.Spin+=Math.Max(-220,Math.Min(220,-x*speed*.3));}
    if(!b.Pinned){b.X+=bx*dt*t;b.Y+=by*dt*t;DislodgeBrick(b,x*speed,y*speed);b.VX=bx+jolt*ib*x;b.VY=by+jolt*ib*y;b.Spin+=Math.Max(-220,Math.Min(220,x*speed*.3));}
    if(Sound!=null)Sound("ball",Math.Min(a.Area,b.Area));
   }
  }
  void CollideBallsWithBricks(double dt){
   foreach(var ball in Balls.ToArray()){
    if(ball.Held||!ball.Visible||ball.OnBall!=null)continue;
    double vx=ball.VX,vy=ball.Support==null?ball.VY:0,best=2,nx=0,ny=0;SnowBrick target=null;
    foreach(var brick in Bricks){if(brick.Held||brick.Area<20)continue;double t,x,y;
     if(SweepBallBrick(ball,brick,(vx-BrickVelocityX(brick))*dt,(vy-BrickVelocityY(brick))*dt,out t,out x,out y)&&t<best){best=t;target=brick;nx=x;ny=y;}
    }
    if(target==null)continue;double bx=BrickVelocityX(target),by=BrickVelocityY(target),speed=(vx-bx)*nx+(vy-by)*ny;
    if(speed<=1)continue;double ia=1/Math.Max(1,ball.Area),ib=target.Pinned?0:1/target.Area,jolt=1.3*speed/(ia+ib);
    ball.X+=vx*dt*best;ball.Y+=vy*dt*best;
    if(!target.Pinned&&speed>=65){DislodgeBrick(target,nx*speed,ny*speed);target.VX=bx+jolt*ib*nx;target.VY=by+jolt*ib*ny;target.Spin+=Math.Max(-240,Math.Min(240,nx*speed*.4));}
    ball.Support=null;ball.OnBall=null;ball.Face=false;ball.VX=vx-jolt*ia*nx;ball.VY=vy-jolt*ia*ny;
    if(ball.Loose){if(Flakes.Count<EffectiveLimit){ScatterHandfulImpact(ball.Area,ball.X-nx*3,ball.Y-ny*3,ball.VX,ball.VY,target,nx,ny);Balls.Remove(ball);}continue;}
    if(ball.ThrowArmed&&speed>=500){Scatter(ball.Area,ball.X,ball.Y,ball.VX*.2,-Math.Min(220,speed*.2),null);Balls.Remove(ball);foreach(var upper in Balls)if(upper.OnBall==ball){upper.OnBall=null;upper.Support=null;upper.Face=false;}}
    if(Sound!=null&&speed>=65)Sound("ball",ball.Area);
   }
  }
  static bool SweepBallBrick(Ball ball,SnowBrick brick,double dx,double dy,out double t,out double nx,out double ny){
   return SweepSnowBrick(ball.X,ball.Y,ball.Radius,brick,dx,dy,out t,out nx,out ny);
  }
  static bool SweepSnowBrick(double snowX,double snowY,double radius,SnowBrick brick,double dx,double dy,out double t,out double nx,out double ny){
   // Circle against the exact rounded rectangle (faces plus corner arcs).
   double a=brick.Angle*Math.PI/180,c=Math.Cos(a),s=Math.Sin(a),px=(snowX-brick.X)*c+(snowY-brick.Y)*s,py=-(snowX-brick.X)*s+(snowY-brick.Y)*c;
   double vx=dx*c+dy*s,vy=-dx*s+dy*c,hx=brick.Width/2,hy=brick.Height/2,r=radius,best=2,lx=0,ly=0;
   double qx=Math.Max(-hx,Math.Min(hx,px)),qy=Math.Max(-hy,Math.Min(hy,py)),ex=qx-px,ey=qy-py,len=Math.Sqrt(ex*ex+ey*ey);
   if(len<=r){best=0;if(len>1e-8){lx=ex/len;ly=ey/len;}else if(hx-Math.Abs(px)<hy-Math.Abs(py)){lx=px>=0?-1:1;}else ly=py>=0?-1:1;}
   for(int sign=-1;sign<=1;sign+=2){
    if(Math.Abs(vx)>1e-9){double u=(sign*(hx+r)-px)/vx,y=py+vy*u;if(u>=0&&u<=1&&Math.Abs(y)<=hy&&u<best){best=u;lx=-sign;ly=0;}}
    if(Math.Abs(vy)>1e-9){double u=(sign*(hy+r)-py)/vy,x=px+vx*u;if(u>=0&&u<=1&&Math.Abs(x)<=hx&&u<best){best=u;lx=0;ly=-sign;}}
    for(int side=-1;side<=1;side+=2){double u;if(SweepCircle(px,py,px+vx,py+vy,sign*hx,side*hy,r,out u)&&u<best){double x=px+vx*u,y=py+vy*u;if(sign*x>=hx&&side*y>=hy){best=u;lx=(sign*hx-x)/r;ly=(side*hy-y)/r;}}}
   }
   t=best;nx=lx*c-ly*s;ny=lx*s+ly*c;return best<=1;
  }
 }
}
