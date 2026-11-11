using System;
using System.Text;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Diagnostics;

namespace Sneg {
 internal static class Native {
  internal const int Layered=0x80000, Transparent=0x20, NoActivate=0x8000000, ToolWindow=0x80;
  [StructLayout(LayoutKind.Sequential)] internal struct Rect {public int L,T,R,B; public Rectangle Value {get{return Rectangle.FromLTRB(L,T,R,B);}}}
  [StructLayout(LayoutKind.Sequential)] internal struct Pt {public int X,Y;public Pt(int x,int y){X=x;Y=y;}}
  [StructLayout(LayoutKind.Sequential)] internal struct Sz {public int X,Y;public Sz(int x,int y){X=x;Y=y;}}
  [StructLayout(LayoutKind.Sequential,Pack=1)] internal struct Blend {public byte Op,Flags,Alpha,Format;}
  [StructLayout(LayoutKind.Sequential)] internal struct BitmapInfo {public uint Size;public int Width,Height;public ushort Planes,Bits;public uint Compression,ImageSize;public int XP,YP;public uint Used,Important;}
  internal delegate bool EnumProc(IntPtr h,IntPtr p);
  [DllImport("user32.dll")] internal static extern bool EnumWindows(EnumProc cb,IntPtr p);
  [DllImport("user32.dll")] internal static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] internal static extern bool IsWindow(IntPtr h);
  [DllImport("user32.dll")] internal static extern bool IsIconic(IntPtr h);
  [DllImport("user32.dll")] internal static extern bool GetWindowRect(IntPtr h,out Rect r);
  [DllImport("user32.dll",CharSet=CharSet.Unicode)] internal static extern int GetClassName(IntPtr h,StringBuilder s,int n);
  [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(IntPtr h,out uint pid);
  [DllImport("user32.dll",EntryPoint="GetWindowLongW")] internal static extern int GetWindowLong(IntPtr h,int index);
  [DllImport("dwmapi.dll")] internal static extern int DwmGetWindowAttribute(IntPtr h,int a,out Rect r,int size);
  [DllImport("dwmapi.dll",EntryPoint="DwmGetWindowAttribute")] internal static extern int DwmInt(IntPtr h,int a,out int v,int size);
  [DllImport("user32.dll")] internal static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll")] internal static extern bool GetCursorPos(out Pt p);
  [DllImport("user32.dll")] internal static extern short GetAsyncKeyState(int key);
  [DllImport("user32.dll")] internal static extern bool RegisterHotKey(IntPtr h,int id,uint mods,uint key);
  [DllImport("user32.dll")] internal static extern bool UnregisterHotKey(IntPtr h,int id);
  [DllImport("user32.dll")] internal static extern bool ReleaseCapture();
  [DllImport("user32.dll")] internal static extern bool SetWindowPos(IntPtr h,IntPtr after,int x,int y,int cx,int cy,uint flags);
  [DllImport("user32.dll")] internal static extern IntPtr GetDC(IntPtr h);
  [DllImport("user32.dll")] internal static extern int ReleaseDC(IntPtr h,IntPtr dc);
  [DllImport("gdi32.dll")] internal static extern IntPtr CreateCompatibleDC(IntPtr dc);
  [DllImport("gdi32.dll")] internal static extern bool DeleteDC(IntPtr dc);
  [DllImport("gdi32.dll")] internal static extern IntPtr CreateDIBSection(IntPtr dc,ref BitmapInfo info,uint usage,out IntPtr bits,IntPtr section,uint offset);
  [DllImport("gdi32.dll")] internal static extern IntPtr SelectObject(IntPtr dc,IntPtr obj);
  [DllImport("gdi32.dll")] internal static extern bool DeleteObject(IntPtr obj);
  [DllImport("user32.dll",SetLastError=true)] internal static extern bool UpdateLayeredWindow(IntPtr h,IntPtr dst,ref Pt point,ref Sz size,IntPtr source,ref Pt origin,uint key,ref Blend blend,uint flags);
  [DllImport("user32.dll")] internal static extern bool SetProcessDpiAwarenessContext(IntPtr context);
  [DllImport("shcore.dll")] internal static extern int SetProcessDpiAwareness(int awareness);
  [DllImport("winmm.dll")] internal static extern uint timeBeginPeriod(uint ms);
  [DllImport("winmm.dll")] internal static extern uint timeEndPeriod(uint ms);
  [DllImport("user32.dll")] internal static extern int GetGuiResources(IntPtr h,int flag);
  [DllImport("user32.dll")] internal static extern bool DestroyIcon(IntPtr icon);
  internal static void Dpi(){try{if(SetProcessDpiAwarenessContext(new IntPtr(-4)))return;}catch(EntryPointNotFoundException){}try{SetProcessDpiAwareness(2);}catch(DllNotFoundException){}}
  internal static Rectangle Bounds(IntPtr h){Rect r;if(DwmGetWindowAttribute(h,9,out r,16)!=0||r.R<=r.L||r.B<=r.T)GetWindowRect(h,out r);return r.Value;}
 }

 internal sealed class WindowInfo {
  public IntPtr Handle; public Rectangle Bounds; public bool Surface; public uint Pid; public string Class;
 }
 internal sealed class WindowTracker {
  public readonly HashSet<IntPtr> Excluded=new HashSet<IntPtr>();
  public readonly HashSet<IntPtr> NoSupport=new HashSet<IntPtr>();
  public List<WindowInfo> Windows=new List<WindowInfo>();
  public void Refresh(){var result=new List<WindowInfo>();
   Native.EnumWindows(delegate(IntPtr h,IntPtr p){
    if(Excluded.Contains(h)||!Native.IsWindowVisible(h)||Native.IsIconic(h))return true;
    int cloak;if(Native.DwmInt(h,14,out cloak,4)==0&&cloak!=0)return true;
    var s=new StringBuilder(128);Native.GetClassName(h,s,s.Capacity);string cl=s.ToString();
    if(cl=="Progman"||cl=="WorkerW"||cl=="Shell_TrayWnd"||cl=="Shell_SecondaryTrayWnd")return true;
    Rectangle r=Native.Bounds(h);if(r.Width<2||r.Height<2)return true;
    int style=Native.GetWindowLong(h,-16),ex=Native.GetWindowLong(h,-20);uint pid;Native.GetWindowThreadProcessId(h,out pid);
    // Transparent helper layers neither support nor occlude snow.
    if((ex&Native.Transparent)!=0&&(ex&Native.Layered)!=0)return true;
    bool eligible=!NoSupport.Contains(h)&&(style&0xC00000)!=0&&(ex&Native.ToolWindow)==0&&r.Width>=100&&r.Height>=60;
    result.Add(new WindowInfo{Handle=h,Bounds=r,Surface=eligible,Pid=pid,Class=cl});return true;
   },IntPtr.Zero);Windows=result;
  }
  public bool Fullscreen(){IntPtr f=Native.GetForegroundWindow();foreach(var w in Windows)if(w.Handle==f){foreach(var s in System.Windows.Forms.Screen.AllScreens)if(w.Bounds.Left<=s.Bounds.Left&&w.Bounds.Top<=s.Bounds.Top&&w.Bounds.Right>=s.Bounds.Right&&w.Bounds.Bottom>=s.Bounds.Bottom)return true;}return false;}
 }
}
