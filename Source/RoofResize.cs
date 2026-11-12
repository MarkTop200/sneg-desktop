using System;
namespace Sneg {
 internal sealed partial class SnowWorld {
  void ResizeRoof(Surface roof,double left,double width){double oldLeft=roof.Left,oldWidth=roof.Width,cell=roof.Cell;var old=roof.Heights;var oldPending=roof.Pending;double right=left+width,oldRight=oldLeft+oldWidth;
   if(left>oldLeft){CutRoofStrip(roof,old,oldPending,oldLeft,cell,oldLeft,Math.Min(left,oldRight),-1);if(roof.OverflowLeft>0){EmitRoofCut(roof,roof.OverflowLeft,oldLeft,roof.Y-12,-1);roof.OverflowLeft=0;}}
   if(right<oldRight){CutRoofStrip(roof,old,oldPending,oldLeft,cell,Math.Max(right,oldLeft),oldRight,1);if(roof.OverflowRight>0){EmitRoofCut(roof,roof.OverflowRight,oldRight,roof.Y-12,1);roof.OverflowRight=0;}}
   roof.Left=left;roof.Width=Math.Max(1,width);roof.Heights=new double[Math.Max(1,(int)Math.Ceiling(roof.Width/6))];roof.Pending=new double[roof.Heights.Length];double nextCell=roof.Cell;
   for(int j=0;j<roof.Heights.Length;j++){double l=left+j*nextCell,r=l+nextCell,area=0;int a=Math.Max(0,(int)Math.Floor((l-oldLeft)/cell)),b=Math.Min(old.Length-1,(int)Math.Floor((r-oldLeft)/cell));for(int i=a;i<=b;i++)area+=Math.Max(0,Math.Min(r,oldLeft+(i+1)*cell)-Math.Max(l,oldLeft+i*cell))*(old[i]+oldPending[i]/cell);roof.Heights[j]=area/nextCell;}
   foreach(var brick in Bricks)if(brick.Support==roof&&(brick.X<left||brick.X>=right)){brick.Support=null;brick.OnBrick=null;}
  }
  void CutRoofStrip(Surface roof,double[] heights,double[] pending,double left,double cell,double from,double to,int direction){if(to<=from)return;int groups=Math.Min(12,Math.Max(1,(int)Math.Ceiling((to-from)/28)));for(int k=0;k<groups;k++){double l=from+(to-from)*k/groups,r=from+(to-from)*(k+1)/groups,mass=0,x=0,y=0;int a=Math.Max(0,(int)Math.Floor((l-left)/cell)),b=Math.Min(heights.Length-1,(int)Math.Floor((r-left)/cell));for(int i=a;i<=b;i++){double il=Math.Max(l,left+i*cell),ir=Math.Min(r,left+(i+1)*cell),q=Math.Max(0,ir-il)*(heights[i]+pending[i]/cell);mass+=q;x+=(il+ir)*.5*q;y+=(roof.Y-heights[i]*.5)*q;}if(mass>.001)EmitRoofCut(roof,mass,x/mass,y/mass,direction);}}
  void EmitRoofCut(Surface roof,double area,double x,double y,int direction){if(Flakes.Count>=EffectiveLimit){for(int i=Flakes.Count-1;i>=0;i--)if(!Flakes[i].Debris&&!Flakes[i].ImpactDust){Flakes.RemoveAt(i);break;}}if(Flakes.Count<EffectiveLimit)Flakes.Add(CreateClump(area,x,y,roof.Id,direction*(25+random.NextDouble()*55),35+random.NextDouble()*45));else if(Settings.Accumulate)SettleToFloor(x,area,roof);}
 }
}
