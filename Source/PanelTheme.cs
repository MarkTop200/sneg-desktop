using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Sneg {
 internal static class PanelTheme {
  internal static readonly Color Paper=Color.FromArgb(222,234,245),Ink=Color.FromArgb(16,36,79),Purple=Color.FromArgb(88,72,214),Muted=Color.FromArgb(91,112,147),Cyan=Color.FromArgb(15,195,235);
  internal static GraphicsPath Round(RectangleF r,float radius){var p=new GraphicsPath();float d=Math.Min(radius*2,Math.Min(r.Width,r.Height));p.AddArc(r.X,r.Y,d,d,180,90);p.AddArc(r.Right-d,r.Y,d,d,270,90);p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);p.AddArc(r.X,r.Bottom-d,d,d,90,90);p.CloseFigure();return p;}
 }
 internal sealed class SoftButton:Button {
  bool hover,down;public bool Selected;
  public SoftButton(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;}
  protected override void OnMouseEnter(EventArgs e){hover=true;Invalidate();base.OnMouseEnter(e);}
  protected override void OnMouseLeave(EventArgs e){hover=down=false;Invalidate();base.OnMouseLeave(e);}
  protected override void OnMouseDown(MouseEventArgs e){down=true;Invalidate();base.OnMouseDown(e);}
  protected override void OnMouseUp(MouseEventArgs e){down=false;Invalidate();base.OnMouseUp(e);}
  protected override void OnPaint(PaintEventArgs e){var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;g.Clear(Parent==null?PanelTheme.Paper:Parent.BackColor);var rect=new RectangleF(0,0,Width-1,Height-2);Color fill=Selected?PanelTheme.Purple:BackColor;if(down)fill=Color.FromArgb(61,44,165);else if(hover)fill=Selected?Color.FromArgb(105,89,232):Color.FromArgb(204,222,244);using(var path=PanelTheme.Round(rect,16)){using(var b=new SolidBrush(fill))g.FillPath(b,path);if(Focused)using(var pen=new Pen(PanelTheme.Cyan,2)){g.DrawPath(pen,path);}}
   var textRect=new Rectangle(8,0,Width-16,Height-2);if(Image!=null){g.DrawImage(Image,10,(Height-30)/2,30,30);textRect.X=44;textRect.Width=Width-50;}TextRenderer.DrawText(g,Text,Font,textRect,Selected?Color.White:ForeColor,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
  }
 }
 internal sealed class SoftCard:Panel {
  public SoftCard(){DoubleBuffered=true;BackColor=PanelTheme.Paper;Padding=new Padding(12,8,12,6);}
  protected override void OnPaintBackground(PaintEventArgs e){e.Graphics.Clear(Parent==null?PanelTheme.Paper:Parent.BackColor);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;using(var path=PanelTheme.Round(new RectangleF(0,0,Width-1,Height-1),20))using(var b=new SolidBrush(Color.FromArgb(239,246,252)))e.Graphics.FillPath(b,path);}
 }
}
