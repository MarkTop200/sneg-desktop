using System;
namespace Sneg {
 internal sealed partial class Surface {
  public double AvalancheHeight=40;bool[] avalancheCells;
  // Move a visible shoulder only at its empty boundary. Queued overflow travels
  // through the pile one bin at a time, rather than teleporting to a remote gap.
  void RelaxFloor(){int n=Heights.Length;var height=(double[])Heights.Clone();var pending=(double[])Pending.Clone();var next=(double[])height.Clone();var queue=(double[])pending.Clone();
   var destination=new bool[n];for(int i=0;i<n;i++)destination[i]=height[i]<=.05||height[i]<MaxHeight-.05&&(i==0||i==n-1||height[i]<height[i-1]-.05&&height[i]<height[i+1]-.05);var leftGap=new int[n];var rightGap=new int[n];int gap=-1;for(int i=0;i<n;i++){if(destination[i])gap=i;leftGap[i]=gap;}gap=-1;for(int i=n-1;i>=0;i--){if(destination[i])gap=i;rightGap[i]=gap;}
   for(int i=0;i<n;i++)if(pending[i]>.00001){int left=leftGap[i],right=rightGap[i];bool freeLeft=left>=0&&left<i,freeRight=right>=0&&right>i;
    if(destination[i]){double q=Math.Min(pending[i],Math.Min(Cell*1.2,Math.Max(0,MaxHeight-next[i])*Cell));queue[i]-=q;next[i]+=q/Cell;}
    else if(freeLeft||freeRight){int target=freeLeft&&(!freeRight||i-left<=right-i)?i-1:i+1;double q=Math.Min(pending[i],Cell*1.2);queue[i]-=q;queue[target]+=q;}
   }
   var avalanche=new bool[n];for(int first=0;first<n;){if(height[first]<=.05){first++;continue;}int last=first;double peak=height[first];bool wasActive=false;while(last<n&&height[last]>.05){peak=Math.Max(peak,height[last]);if(avalancheCells!=null&&avalancheCells.Length==n&&avalancheCells[last])wasActive=true;last++;}if(peak>=AvalancheHeight||wasActive&&peak>AvalancheHeight*.72)for(int j=Math.Max(0,first-1);j<=Math.Min(n-1,last);j++)avalanche[j]=true;first=last;}avalancheCells=avalanche;
   for(int i=0;i<n-1;i++){int from=height[i]>=height[i+1]?i:i+1,to=from==i?i+1:i;bool edge=height[to]<=.05||(to<from?to>0&&height[to-1]<=.05:to<n-1&&height[to+1]<=.05);if(!edge)for(int margin=1;margin<=3;margin++){int left=to-margin,right=to+margin;if(left<0||right>=n||height[Math.Max(0,left)]<=.05||height[Math.Min(n-1,right)]<=.05){edge=true;break;}}bool sliding=avalanche[from]&&avalanche[to];if(!edge&&!sliding)continue;double difference=height[from]-height[to]-Cell*(sliding?.35:.65);if(difference<=0)continue;double q=Math.Min(sliding?3.8:1.2,difference*(sliding?.22:.12));q=Math.Min(q,Math.Min(next[from],Math.Max(0,MaxHeight-next[to])));next[from]-=q;next[to]+=q;}
   Heights=next;Pending=queue;
  }
 }
}


