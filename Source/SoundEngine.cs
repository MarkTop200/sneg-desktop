using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace Sneg {
 // A private PCM stream: never changes the Windows master volume.
 internal sealed class SoundEngine:IDisposable {
  [StructLayout(LayoutKind.Sequential)] struct Format {public ushort tag,channels;public uint rate,bytes;public ushort align,bits,extra;}
  [StructLayout(LayoutKind.Sequential)] struct Header {public IntPtr data;public uint length,recorded;public UIntPtr user;public uint flags,loops;public IntPtr next;public UIntPtr reserved;}
  [DllImport("winmm.dll")] static extern uint waveOutOpen(out IntPtr h,uint id,ref Format f,IntPtr cb,IntPtr instance,uint flags);
  [DllImport("winmm.dll")] static extern uint waveOutPrepareHeader(IntPtr h,IntPtr header,uint size);
  [DllImport("winmm.dll")] static extern uint waveOutWrite(IntPtr h,IntPtr header,uint size);
  [DllImport("winmm.dll")] static extern uint waveOutReset(IntPtr h);
  [DllImport("winmm.dll")] static extern uint waveOutUnprepareHeader(IntPtr h,IntPtr header,uint size);
  [DllImport("winmm.dll")] static extern uint waveOutClose(IntPtr h);
  readonly object gate=new object();readonly Settings settings;Thread worker;volatile bool stop;public string Error="";public bool Opened;public int Submitted;
  bool active,plow;double wind,engine,master,windForce,fallSpeed,ball,shovel,mitten,low,phase;uint seed=7251;
  public SoundEngine(Settings s,bool device){settings=s;if(device){worker=new Thread(Run){IsBackground=true,Name="Sneg audio"};worker.Start();}}
  public void Update(SnowWorld w,bool enabled){lock(gate){active=enabled&&!w.Paused;plow=w.Plowing;master=settings.SoundEnabled&&active?settings.SoundVolume/100.0:0;windForce=Math.Min(1,Math.Abs(w.Settings.Wind)/250.0);fallSpeed=w.Settings.Speed/100.0;double intensity=w.Settings.Intensity;int v=intensity<=60?settings.LightSound:intensity<=160?settings.NormalSound:settings.BlizzardSound;wind=w.Emitting?v/100.0*Math.Min(1,intensity/160.0):0;engine=plow?settings.PlowSound/100.0:0;if(master==0)ball=shovel=mitten=0;}}
  public void Trigger(string kind,double amount){lock(gate){if(!settings.SoundEnabled||!active)return;double strength=Math.Min(1,.2+Math.Sqrt(Math.Max(0,amount))/65);if(kind=="ball")ball=Math.Max(ball,strength*settings.BallSound/100.0);if(kind=="shovel")shovel=Math.Max(shovel,strength*settings.ShovelSound/100.0);if(kind=="mitten")mitten=Math.Max(mitten,strength*settings.MittenSound/100.0);}}
  internal short[] Samples(int count){var data=new short[count];lock(gate){for(int i=0;i<count;i++){seed=1664525*seed+1013904223;double n=((seed>>8)/16777215.0)*2-1;low+=.035*(n-low);phase+=1.0/22050;double gust=.7+.3*Math.Sin(phase*(.63+windForce*1.5));double motor=Math.Sin(phase*2*Math.PI*62)*.48+Math.Sin(phase*2*Math.PI*124)*.15+n*.12;double value=wind*(low*(1.2+windForce)+n*(.015+.01*fallSpeed))*gust*.3+engine*motor*.19+ball*(n*.12+low*.7)+shovel*(n*.16+Math.Sin(phase*2*Math.PI*620)*.025)+mitten*low*.9;data[i]=(short)(Math.Max(-.9,Math.Min(.9,value*master))*32767);ball*=.99955;shovel*=.9997;mitten*=.9996;}}return data;}
  void Run(){IntPtr device=IntPtr.Zero;var headers=new IntPtr[4];var buffers=new IntPtr[4];uint size=(uint)Marshal.SizeOf(typeof(Header));try{var format=new Format{tag=1,channels=1,rate=22050,bytes=44100,align=2,bits=16};uint result=waveOutOpen(out device,0xffffffff,ref format,IntPtr.Zero,IntPtr.Zero,0);if(result!=0){Error="Не удалось открыть аудиоустройство ("+result+").";return;}Opened=true;for(int i=0;i<4;i++){buffers[i]=Marshal.AllocHGlobal(882);headers[i]=Marshal.AllocHGlobal((int)size);Marshal.StructureToPtr(new Header{data=buffers[i],length=882},headers[i],false);if(waveOutPrepareHeader(device,headers[i],size)!=0)throw new InvalidOperationException("Подготовка звука не удалась.");}
    while(!stop){for(int i=0;i<4&&!stop;i++){var h=(Header)Marshal.PtrToStructure(headers[i],typeof(Header));if((h.flags&16)!=0&&(h.flags&1)==0)continue;var data=Samples(441);Marshal.Copy(data,0,buffers[i],data.Length);if(waveOutWrite(device,headers[i],size)!=0)throw new InvalidOperationException("Вывод звука прерван.");Submitted++;}Thread.Sleep(5);}
   }catch(Exception e){Error=e.Message;}finally{if(device!=IntPtr.Zero)waveOutReset(device);for(int i=0;i<4;i++){if(headers[i]!=IntPtr.Zero){if(device!=IntPtr.Zero)waveOutUnprepareHeader(device,headers[i],size);Marshal.FreeHGlobal(headers[i]);}if(buffers[i]!=IntPtr.Zero)Marshal.FreeHGlobal(buffers[i]);}if(device!=IntPtr.Zero)waveOutClose(device);Opened=false;}}
  public void Dispose(){stop=true;if(worker!=null){worker.Join(1500);worker=null;}}
 }
}
