using System;
using System.IO;
using System.Xml.Serialization;

namespace Sneg {
 public sealed class Settings {
  public int Intensity=90, Size=100, Speed=100, Wind=12, MaxParticles=2000, Fps=60, HotKey=83;
  public bool SoundEnabled=true;
  public int SoundVolume=35,LightSound=35,NormalSound=55,BlizzardSound=80,PlowSound=65,BallSound=65,ShovelSound=65,MittenSound=50;
  public int FloorAvalancheHeight=40;
  public bool MouseShake=true;
  public bool FillDesktop=false;
  public bool Accumulate=true, PauseFullscreen=true, Diagnostics=false;
  public static Settings Load(string path){try{using(var s=File.OpenRead(path)){var v=(Settings)new XmlSerializer(typeof(Settings)).Deserialize(s);v.Validate();return v;}}catch(Exception e){if(e is IOException||e is InvalidOperationException||e is UnauthorizedAccessException)return new Settings();throw;}}
  public void Validate(){FloorAvalancheHeight=Clamp(FloorAvalancheHeight,10,60);SoundVolume=Clamp(SoundVolume,0,100);LightSound=Clamp(LightSound,0,100);NormalSound=Clamp(NormalSound,0,100);BlizzardSound=Clamp(BlizzardSound,0,100);PlowSound=Clamp(PlowSound,0,100);BallSound=Clamp(BallSound,0,100);ShovelSound=Clamp(ShovelSound,0,100);MittenSound=Clamp(MittenSound,0,100);Intensity=Clamp(Intensity,0,350);Size=Clamp(Size,40,220);Speed=Clamp(Speed,30,250);Wind=Clamp(Wind,-250,250);MaxParticles=Clamp(MaxParticles,100,5000);Fps=Fps==30?30:60;if(HotKey<65||HotKey>90)HotKey=83;}
  public void Save(string path){Validate();string tmp=path+".tmp";using(var s=File.Create(tmp))new XmlSerializer(typeof(Settings)).Serialize(s,this);if(File.Exists(path)){File.Replace(tmp,path,null);}else File.Move(tmp,path);}
  static int Clamp(int x,int a,int b){return Math.Max(a,Math.Min(b,x));}
 }
}
