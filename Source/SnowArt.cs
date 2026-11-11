using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace Sneg {
 internal static class SnowArt {
  internal static readonly Bitmap Grain=CreateGrain(),Plow=CreatePlow();
  static Bitmap CreateGrain(){
   var image=new Bitmap(256,256,PixelFormat.Format32bppPArgb);var rnd=new Random(2404);var coarse=new double[17,17];
   for(int y=0;y<16;y++)for(int x=0;x<16;x++)coarse[x,y]=rnd.NextDouble()*2-1;
   for(int i=0;i<17;i++){coarse[16,i%16]=coarse[0,i%16];coarse[i%16,16]=coarse[i%16,0];}coarse[16,16]=coarse[0,0];
   for(int y=0;y<256;y++)for(int x=0;x<256;x++){int ix=x/16,iy=y/16;double tx=(x%16)/16.0,ty=(y%16)/16.0;tx=tx*tx*(3-2*tx);ty=ty*ty*(3-2*ty);double top=coarse[ix,iy]*(1-tx)+coarse[ix+1,iy]*tx,bottom=coarse[ix,iy+1]*(1-tx)+coarse[ix+1,iy+1]*tx;double n=(top*(1-ty)+bottom*ty)*3+(rnd.NextDouble()+rnd.NextDouble()-1)*5;int alpha=Math.Min(56,6+(int)(Math.Abs(n)*7));image.SetPixel(x,y,n<0?Color.FromArgb(alpha,155,178,200):Color.FromArgb(alpha,255,255,255));}
   using(var g=Graphics.FromImage(image)){g.SmoothingMode=SmoothingMode.AntiAlias;for(int i=0;i<900;i++){float x=rnd.Next(256),y=rnd.Next(256),r=(float)(.4+rnd.NextDouble()*1.2);using(var b=new SolidBrush(i%3==0?Color.FromArgb(35,123,154,183):Color.FromArgb(45,255,255,255)))g.FillEllipse(b,x,y,r,r);}}
   return image;
  }
  internal static void GrainFill(Graphics g,GraphicsPath path,float anchorX,float anchorY){using(var texture=new TextureBrush(Grain,WrapMode.Tile)){texture.TranslateTransform(anchorX,anchorY);g.FillPath(texture,path);}}
  internal static void Drift(Graphics g,GraphicsPath path,Surface surface){
   using(var light=new LinearGradientBrush(new PointF(0,Math.Min((float)surface.Y-65,path.GetBounds().Top)),new PointF(0,(float)surface.Y+32),Color.FromArgb(255,254,255,255),Color.FromArgb(204,219,236)))g.FillPath(light,path);
   GrainFill(g,path,(float)surface.Left,(float)surface.Y-64);
   var state=g.Save();g.SetClip(path,CombineMode.Intersect);using(var rim=new Pen(Color.FromArgb(100,255,255,255),1.2f))g.DrawPath(rim,path);g.Restore(state);
  }
  internal static void Sphere(Graphics g,float x,float y,float radius){
   if(radius<=0)return;using(var shape=new GraphicsPath()){shape.AddEllipse(x-radius,y-radius,radius*2,radius*2);using(var lighting=new PathGradientBrush(shape)){lighting.CenterPoint=new PointF(x-radius*.28f,y-radius*.34f);lighting.CenterColor=Color.FromArgb(255,255,255,255);lighting.SurroundColors=new[]{Color.FromArgb(187,207,229)};lighting.FocusScales=new PointF(.22f,.22f);g.FillPath(lighting,shape);}GrainFill(g,shape,x-radius,y-radius);
    var state=g.Save();g.SetClip(shape,CombineMode.Intersect);using(var shadow=new LinearGradientBrush(new PointF(0,y-radius),new PointF(0,y+radius),Color.Transparent,Color.FromArgb(55,100,139,185)))g.FillEllipse(shadow,x-radius,y-radius,radius*2,radius*2);g.Restore(state);
    using(var rim=new Pen(Color.FromArgb(85,248,253,255),.8f))g.DrawPath(rim,shape);
   }
  }
  internal static void Heap(Graphics g,Flake flake){
   float r=(float)flake.Radius;if(r<2){using(var b=new SolidBrush(Color.White))g.FillEllipse(b,(float)flake.X-r,(float)flake.Y-r,r*2,r*2);return;}
   var saved=g.Save();g.TranslateTransform((float)flake.X,(float)flake.Y);g.RotateTransform((float)flake.Spin);
   var points=new PointF[12];for(int i=0;i<points.Length;i++){double a=i*Math.PI*2/points.Length,rough=1+.12*Math.Sin(i*2.7+flake.Phase)+.065*Math.Cos(i*4.3+flake.Phase*2);points[i]=new PointF((float)(Math.Cos(a)*r*1.1*rough),(float)(Math.Sin(a)*r*.87*rough));}
   using(var shape=new GraphicsPath()){shape.AddClosedCurve(points,.42f);using(var light=new PathGradientBrush(shape)){light.CenterPoint=new PointF(-r*.25f,-r*.3f);light.CenterColor=Color.FromArgb(255,254,255,255);light.SurroundColors=new[]{Color.FromArgb(184,206,230)};g.FillPath(light,shape);}GrainFill(g,shape,-r,-r);using(var edge=new Pen(Color.FromArgb(100,246,252,255),.8f))g.DrawPath(edge,shape);}
   g.Restore(saved);
  }
  internal static void Snowman(Graphics g,Ball head){
   var middle=head.OnBall;if(middle==null)return;float x=(float)head.X,y=(float)head.Y,r=(float)head.Radius,mr=(float)middle.Radius,my=(float)middle.Y;
   // Coal eyes and seven little pieces in the smile, carrot points left like the reference.
   for(int side=-1;side<=1;side+=2){float ex=x+side*r*.26f;g.FillEllipse(Brushes.SlateGray,ex-r*.1f,y-r*.25f,r*.22f,r*.25f);g.FillEllipse(Brushes.Black,ex-r*.08f,y-r*.25f,r*.17f,r*.2f);g.FillEllipse(Brushes.WhiteSmoke,ex-r*.04f,y-r*.24f,r*.055f,r*.055f);}
   using(var carrot=new LinearGradientBrush(new RectangleF(x-r,y-r*.02f,r,r*.27f),Color.FromArgb(248,174,54),Color.FromArgb(201,83,14),90f))g.FillPolygon(carrot,new[]{new PointF(x-r*.92f,y+r*.16f),new PointF(x-r*.06f,y-r*.035f),new PointF(x-r*.04f,y+r*.2f)});
   for(int i=0;i<7;i++){double a=(25+i*22)*Math.PI/180;float cx=x+(float)Math.Cos(a)*r*.35f,cy=y+r*.21f+(float)Math.Sin(a)*r*.2f;g.FillEllipse(Brushes.MidnightBlue,cx-r*.043f,cy-r*.043f,r*.086f,r*.086f);}
   float scarfY=y+r*.77f,bandHeight=Math.Max(4,r*.26f),tailX=x+r*.37f,tailWidth=Math.Max(5,r*.27f),tailBottom=my+mr*.2f;
   using(var scarf=new GraphicsPath()){scarf.AddBezier(x-r*.84f,scarfY,x-r*.2f,scarfY-4,x+r*.6f,scarfY-3,x+r*.85f,scarfY+1);scarf.AddLine(x+r*.85f,scarfY+1,x+r*.8f,scarfY+bandHeight);scarf.AddBezier(x+r*.8f,scarfY+bandHeight,x+r*.1f,scarfY+bandHeight-1,x-r*.45f,scarfY+bandHeight+2,x-r*.85f,scarfY+bandHeight);scarf.CloseFigure();using(var red=new LinearGradientBrush(new PointF(x,scarfY),new PointF(x,scarfY+bandHeight),Color.FromArgb(234,36,45),Color.FromArgb(158,12,30)))g.FillPath(red,scarf);}
   using(var red=new LinearGradientBrush(new RectangleF(tailX,scarfY,tailWidth,Math.Max(1,tailBottom-scarfY)),Color.FromArgb(220,31,45),Color.FromArgb(163,13,31),0f))g.FillPolygon(red,new[]{new PointF(tailX,scarfY+bandHeight*.4f),new PointF(tailX+tailWidth,scarfY+bandHeight*.4f),new PointF(tailX+tailWidth,tailBottom),new PointF(tailX+tailWidth*.45f,tailBottom-2),new PointF(tailX,tailBottom)});
   using(var stripe=new Pen(Color.FromArgb(244,218,168),Math.Max(1,r*.055f))){g.DrawLine(stripe,tailX,tailBottom-r*.14f,tailX+tailWidth,tailBottom-r*.14f);g.DrawLine(stripe,tailX,tailBottom-r*.27f,tailX+tailWidth,tailBottom-r*.27f);}
   for(int i=0;i<3;i++){float br=Math.Max(1.6f,mr*.09f),by=my-mr*.32f+i*mr*.34f;g.FillEllipse(Brushes.SlateGray,x-br,by-br,br*2+1,br*2+1);g.FillEllipse(Brushes.Black,x-br,by-br,br*2,br*2);}
  }
  static Bitmap CreatePlow(){
   var image=new Bitmap(256,176,PixelFormat.Format32bppPArgb);using(var g=Graphics.FromImage(image)){g.SmoothingMode=SmoothingMode.AntiAlias;g.ScaleTransform(2,2);
    using(var b=new SolidBrush(Color.FromArgb(60,13,29,45)))g.FillEllipse(b,10,77,112,10);
    using(var track=new GraphicsPath()){track.AddArc(13,62,24,24,90,180);track.AddArc(77,62,24,24,270,180);track.CloseFigure();using(var rubber=new LinearGradientBrush(new Rectangle(13,62,90,24),Color.FromArgb(57,63,69),Color.FromArgb(14,20,29),90f))g.FillPath(rubber,track);using(var p=new Pen(Color.FromArgb(3,10,17),2))g.DrawPath(p,track);}
    for(int i=0;i<6;i++){float x=23+i*13;g.FillEllipse(Brushes.SlateGray,x-7,67,14,14);g.FillEllipse(Brushes.LightSlateGray,x-5,69,10,10);g.FillEllipse(Brushes.Gainsboro,x-2,72,4,4);}
    g.FillRectangle(Brushes.DarkSlateGray,17,57,85,10);
    using(var body=new GraphicsPath()){body.AddPolygon(new[]{new PointF(18,43),new PointF(84,39),new PointF(105,47),new PointF(103,64),new PointF(19,66)});using(var yellow=new LinearGradientBrush(new Rectangle(18,39,88,28),Color.FromArgb(255,218,69),Color.FromArgb(224,155,18),90f))g.FillPath(yellow,body);using(var p=new Pen(Color.FromArgb(151,95,9),1.4f))g.DrawPath(p,body);}
    g.FillPolygon(Brushes.Goldenrod,new[]{new PointF(84,39),new PointF(105,47),new PointF(105,63),new PointF(84,59)});
    using(var glass=new GraphicsPath()){glass.AddPolygon(new[]{new PointF(43,24),new PointF(70,21),new PointF(82,44),new PointF(43,46)});using(var blue=new LinearGradientBrush(new Rectangle(43,21,40,26),Color.FromArgb(136,219,246),Color.FromArgb(41,123,177),65f))g.FillPath(blue,glass);using(var p=new Pen(Color.FromArgb(243,182,31),3))g.DrawPath(p,glass);}
    using(var p=new Pen(Color.FromArgb(183,117,17),3))g.DrawLine(p,65,23,69,44);using(var p=new Pen(Color.FromArgb(180,228,250,255),1.2f))g.DrawLine(p,47,29,60,25);
    g.FillRectangle(Brushes.Gold,39,17,38,7);g.FillRectangle(Brushes.Goldenrod,39,23,39,2);g.FillRectangle(Brushes.DarkOrange,56,11,9,6);g.FillEllipse(Brushes.Orange,56,9,9,5);
    using(var p=new Pen(Color.FromArgb(110,75,23),1.2f))for(int i=0;i<4;i++)g.DrawLine(p,88+i*3,49,88+i*3,58);
    g.FillRectangle(Brushes.LightYellow,102,47,5,6);g.FillRectangle(Brushes.OrangeRed,18,48,4,5);
    using(var p=new Pen(Color.FromArgb(112,127,139),5))g.DrawLine(p,97,64,114,68);
    using(var blade=new GraphicsPath()){blade.AddBezier(111,35,121,35,126,48,122,82);blade.AddLine(122,82,109,79);blade.AddBezier(109,79,113,57,112,43,111,35);blade.CloseFigure();using(var metal=new LinearGradientBrush(new Rectangle(108,35,18,48),Color.FromArgb(249,252,255),Color.FromArgb(139,160,180),0f))g.FillPath(metal,blade);using(var p=new Pen(Color.FromArgb(91,113,136),1.4f))g.DrawPath(p,blade);using(var p=new Pen(Color.White,2))g.DrawBezier(p,113,37,118,44,121,54,118,77);}
    using(var snow=new SolidBrush(Color.FromArgb(235,249,252,255)))g.FillEllipse(snow,103,77,25,7);
   }return image;
  }
  internal static void Vehicle(Graphics g,float x,float ground,double time,int direction){var mirror=g.Save();g.TranslateTransform(x,ground);g.ScaleTransform(direction,1);Vehicle(g,0,0,time);g.Restore(mirror);}
  internal static void Vehicle(Graphics g,float x,float ground,double time){g.DrawImage(Plow,x-64,ground-88,128,88);var saved=g.Save();g.SetClip(new RectangleF(x-49,ground-25,88,21),CombineMode.Intersect);using(var tread=new Pen(Color.FromArgb(130,103,112,120),1.2f)){float phase=(float)(time*110%9);for(float k=-52-phase;k<45;k+=9)g.DrawLine(tread,x+k,ground-7,x+k+4,ground-3);}g.Restore(saved);}
 }
}

