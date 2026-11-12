using System;
using System.Runtime.InteropServices;

namespace Sneg {
 internal sealed class BrickInput:IDisposable {
  delegate IntPtr Hook(int code,IntPtr message,IntPtr data);
  [StructLayout(LayoutKind.Sequential)] struct MouseData {public Native.Pt point;public uint mouseData,flags,time;public UIntPtr extra;}
  [DllImport("user32.dll",SetLastError=true)] static extern IntPtr SetWindowsHookEx(int id,Hook callback,IntPtr module,uint thread);
  [DllImport("user32.dll")] static extern bool UnhookWindowsHookEx(IntPtr handle);
  [DllImport("user32.dll")] static extern IntPtr CallNextHookEx(IntPtr handle,int code,IntPtr message,IntPtr data);
  [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] static extern IntPtr GetModuleHandle(string name);
  readonly SnowApp app;readonly Hook callback;IntPtr hook,command;bool held,pinKey;readonly System.Text.StringBuilder journal=new System.Text.StringBuilder();
  public BrickInput(SnowApp owner){app=owner;callback=Handle;}
  public void Sync(IntPtr commandWindow){command=commandWindow;bool now=app.Enabled&&!app.SystemPause&&!app.World.Paused&&app.World.HeldBrick!=null;if(now==held)return;held=now;if(now){pinKey=Native.RegisterHotKey(commandWindow,4,0x4000,70);hook=SetWindowsHookEx(14,callback,GetModuleHandle(null),0);if(app.Lab)journal.AppendLine("Held: tool="+app.World.Tool+"; F="+pinKey+"; wheelHook="+(hook!=IntPtr.Zero));if(!pinKey)app.Notice="Клавиша F занята: закрепление недоступно, отпустите кирпичик и освободите F.";if(hook==IntPtr.Zero)app.Notice="Не удалось включить поворот колёсиком.";}else{Native.UnregisterHotKey(commandWindow,4);pinKey=false;if(hook!=IntPtr.Zero){UnhookWindowsHookEx(hook);hook=IntPtr.Zero;}}}
  IntPtr Handle(int code,IntPtr message,IntPtr data){if(code>=0&&message.ToInt32()==0x20A&&app.World.HeldBrick!=null&&app.Enabled&&!app.SystemPause&&!app.World.Paused){var m=(MouseData)Marshal.PtrToStructure(data,typeof(MouseData));app.World.RotateBrick(unchecked((short)(m.mouseData>>16)));if(app.Lab)journal.AppendLine("Wheel angle="+app.World.HeldBrick.Angle);return new IntPtr(1);}return CallNextHookEx(hook,code,message,data);}
  public void RecordPin(){if(app.Lab&&app.World.HeldBrick!=null)journal.AppendLine("F pinned="+app.World.HeldBrick.Pinned);}
  public void Dispose(){if(pinKey&&command!=IntPtr.Zero)Native.UnregisterHotKey(command,4);pinKey=false;held=false;if(hook!=IntPtr.Zero){UnhookWindowsHookEx(hook);hook=IntPtr.Zero;}if(app.Lab)try{journal.AppendLine("Passive brick grabs="+app.World.PassiveBrickGrabs);System.IO.File.WriteAllText(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"LabData","brick-input-test.txt"),journal.ToString());}catch(System.IO.IOException){}}
 }
}
