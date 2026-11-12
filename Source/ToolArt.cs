using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace Sneg {
 internal static class ToolArt {
  internal static readonly Bitmap Mitten=Create(Tool.Mitten),Shovel=Create(Tool.Shovel);
  internal static readonly Bitmap MittenIcon=new Bitmap(Mitten,30,34),ShovelIcon=new Bitmap(Shovel,30,34);
  static Bitmap Create(Tool tool){var bmp=new Bitmap(128,144,PixelFormat.Format32bppPArgb);using(var g=Graphics.FromImage(bmp)){g.SmoothingMode=SmoothingMode.AntiAlias;g.Clear(Color.Transparent);if(tool==Tool.Mitten)DrawMitten(g);else DrawShovel(g);}return bmp;}
  static void DrawMitten(Graphics g){
   using(var shape=new GraphicsPath()){
    shape.AddBezier(39,111,28,100,22,84,24,65);shape.AddBezier(24,65,12,63,7,52,12,43);shape.AddBezier(12,43,20,32,28,40,36,49);shape.AddBezier(36,49,32,23,39,9,58,8);shape.AddBezier(58,8,82,6,93,24,95,46);shape.AddBezier(95,46,100,70,93,95,83,113);shape.CloseFigure();
    using(var shadow=new Pen(Color.FromArgb(90,4,17,30),7))g.DrawPath(shadow,shape);
    using(var wool=new LinearGradientBrush(new Rectangle(20,8,80,110),Color.FromArgb(93,185,194),Color.FromArgb(22,88,112),28))g.FillPath(wool,shape);
    var clip=g.Save();g.SetClip(shape);
    using(var dark=new Pen(Color.FromArgb(80,8,56,75),1.3f))using(var light=new Pen(Color.FromArgb(130,174,232,220),1.1f)){
     for(int y=13;y<116;y+=7)for(int x=10;x<103;x+=7){float xx=x+((y/7)%2)*3;g.DrawLines(dark,new[]{new PointF(xx-2,y-2),new PointF(xx,y+2),new PointF(xx+2,y-2)});g.DrawLine(light,xx-2,y-3,xx,y+1);}
     for(int x=47;x<=76;x+=14)for(int y=18;y<106;y+=13){g.DrawBezier(light,x,y,x-9,y+4,x+9,y+9,x,y+13);g.DrawBezier(dark,x+2,y,x+11,y+5,x-7,y+10,x+2,y+13);}
    }g.Restore(clip);using(var seam=new Pen(Color.FromArgb(12,69,89),2))g.DrawPath(seam,shape);
   }
   using(var cuff=new GraphicsPath()){cuff.AddPolygon(new[]{new PointF(35,106),new PointF(85,108),new PointF(82,134),new PointF(33,131)});using(var b=new LinearGradientBrush(new Rectangle(33,106,52,28),Color.FromArgb(216,177,104),Color.FromArgb(153,110,55),90))g.FillPath(b,cuff);var state=g.Save();g.SetClip(cuff);using(var p=new Pen(Color.FromArgb(125,105,67,28),1.5f))for(int x=35;x<87;x+=5)g.DrawLine(p,x,107,x-2,135);g.Restore(state);using(var p=new Pen(Color.FromArgb(85,67,42),2))g.DrawPath(p,cuff);}
   using(var p=new Pen(Color.FromArgb(160,231,250,255),2)){g.DrawLine(p,57,44,57,65);g.DrawLine(p,47,54,67,54);g.DrawLine(p,50,47,64,61);g.DrawLine(p,64,47,50,61);}
  }
  static void DrawShovel(Graphics g){
   using(var p=new Pen(Color.FromArgb(75,4,17,30),13)){p.StartCap=p.EndCap=LineCap.Round;g.DrawLine(p,70,29,58,105);}
   using(var wood=new LinearGradientBrush(new Rectangle(56,23,17,85),Color.FromArgb(233,180,100),Color.FromArgb(125,74,35),0f))g.FillPolygon(wood,new[]{new PointF(66,22),new PointF(76,24),new PointF(63,109),new PointF(54,107)});
   using(var grain=new Pen(Color.FromArgb(130,104,56,26),1)){g.DrawBezier(grain,70,32,64,47,68,63,59,98);g.DrawBezier(grain,73,35,68,56,67,75,62,101);}
   using(var handle=new GraphicsPath(FillMode.Alternate)){handle.AddEllipse(49,5,46,35);handle.AddEllipse(58,12,28,19);using(var b=new LinearGradientBrush(new Rectangle(49,5,46,35),Color.FromArgb(231,175,91),Color.FromArgb(131,71,32),90))g.FillPath(b,handle);using(var p=new Pen(Color.FromArgb(87,51,27),2))g.DrawPath(p,handle);}
   using(var blade=new GraphicsPath()){
    blade.AddBezier(29,89,44,81,79,84,95,96);blade.AddLine(95,96,88,129);blade.AddBezier(88,129,72,142,42,141,27,128);blade.CloseFigure();
    using(var p=new Pen(Color.FromArgb(95,5,18,33),6))g.DrawPath(p,blade);
    using(var steel=new LinearGradientBrush(new Rectangle(25,84,72,56),Color.FromArgb(190,221,235),Color.FromArgb(43,92,121),70))g.FillPath(steel,blade);
    var state=g.Save();g.SetClip(blade);
    using(var ribs=new Pen(Color.FromArgb(110,28,70,98),2.5f)){g.DrawLine(ribs,46,91,44,129);g.DrawLine(ribs,62,92,61,133);g.DrawLine(ribs,78,94,78,128);}
    using(var scratches=new Pen(Color.FromArgb(110,225,246,250),.9f)){var rnd=new Random(74);for(int i=0;i<45;i++){int x=rnd.Next(29,93),y=rnd.Next(90,136);g.DrawLine(scratches,x,y,x+rnd.Next(2,8),y-rnd.Next(1,4));}}
    using(var snow=new SolidBrush(Color.FromArgb(218,245,251,255)))g.FillEllipse(snow,24,124,66,19);g.Restore(state);
    using(var p=new Pen(Color.FromArgb(32,68,91),2))g.DrawPath(p,blade);using(var p=new Pen(Color.FromArgb(225,216,237,246),2))g.DrawBezier(p,29,89,44,81,79,84,95,96);
   }
   g.FillEllipse(Brushes.LightSteelBlue,57,95,7,7);g.DrawEllipse(Pens.SlateGray,57,95,7,7);
  }
  internal static void DrawPointer(Graphics g,SnowWorld world){if(world.ShowTool&&world.Tool==Tool.Brick){if(world.HeldBrick==null)BrickArt.DrawMould(g,world.Pointer.X,world.Pointer.Y,58,27,0);return;}if(!world.ShowTool||(world.Tool!=Tool.Mitten&&world.Tool!=Tool.Shovel&&world.Tool!=Tool.Handful))return;var save=g.Save();
   float radius=(world.Tool==Tool.Mitten||world.Tool==Tool.Handful)?24:65;using(var p=new Pen(Color.FromArgb(world.ToolDown?125:65,180,230,246),1)){p.DashStyle=DashStyle.Dot;g.DrawEllipse(p,world.Pointer.X-radius,world.Pointer.Y-radius,radius*2,radius*2);}
   g.TranslateTransform(world.Pointer.X+7,world.Pointer.Y+4);g.RotateTransform(world.ToolDown?(float)(Math.Sin(world.Time*16)*5):(world.Tool==Tool.Mitten||world.Tool==Tool.Handful)?-12:12);
   var img=(world.Tool==Tool.Mitten||world.Tool==Tool.Handful)?Mitten:Shovel;g.DrawImage(img,(world.Tool==Tool.Mitten||world.Tool==Tool.Handful)?-33:-38,(world.Tool==Tool.Mitten||world.Tool==Tool.Handful)?-67:-85,(world.Tool==Tool.Mitten||world.Tool==Tool.Handful)?66:76,(world.Tool==Tool.Mitten||world.Tool==Tool.Handful)?74:86);g.Restore(save);
  }
 }
}
