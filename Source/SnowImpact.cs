using System;
namespace Sneg {
 internal sealed partial class Surface {
  // A falling heap spreads over a footprint proportional to its amount.
  // A cosine profile has a soft edge and conserves the full two-dimensional area.
  double DepositImpact(double x,double area){double radius=Math.Max(24,Math.Sqrt(area)*3);int from=Index(x-radius),to=Index(x+radius);double sum=0;for(int i=from;i<=to;i++){double d=Math.Abs(Left+(i+.5)*Cell-x)/radius;if(d<1)sum+=(1+Math.Cos(d*Math.PI))*.5;}if(sum<=0){Pending[Index(x)]+=area;return area;}
   for(int i=from;i<=to;i++){double d=Math.Abs(Left+(i+.5)*Cell-x)/radius;if(d>=1)continue;double amount=area*((1+Math.Cos(d*Math.PI))*.5)/sum,put=Math.Min(amount,Math.Max(0,MaxHeight-Heights[i])*Cell);Heights[i]+=put/Cell;Pending[i]+=amount-put;}return area;}
 }
 internal sealed partial class SnowWorld {
  void LandHeap(Surface floor,Flake heap,double x,double mass){int count=Math.Min(16,Math.Max(0,EffectiveLimit-Flakes.Count+1));if(count<4||mass<120){floor.Deposit(x,mass,true);return;}double dust=mass*.28;floor.Deposit(x,mass-dust,true);for(int k=0;k<count;k++){double amount=dust/count,vx=heap.VX*.15+(random.NextDouble()-.5)*190,vy=-Math.Min(130,35+Math.Abs(heap.VY)*.07)*( .65+random.NextDouble()*.5);double px=Math.Max(floor.Left+.01,Math.Min(floor.Left+floor.Width-.01,x+(random.NextDouble()-.5)*18));var particle=CreateClump(amount,px,floor.Y-floor.Height(px)-ClumpRadius(amount)-6,floor.Id,vx,vy);particle.ImpactDust=true;particle.IgnoreUntil=.08;Flakes.Add(particle);}}
 }
}
