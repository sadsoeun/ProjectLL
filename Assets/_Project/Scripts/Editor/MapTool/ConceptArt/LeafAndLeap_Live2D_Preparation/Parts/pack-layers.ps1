$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing.Common,System.Drawing.Primitives,System.Runtime.InteropServices,System.Private.Windows.GdiPlus,System.Private.Windows.Core -TypeDefinition @'
using System;
using System.IO;
using System.Text;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Collections.Generic;
public class CubismLayerPack {
 public class Layer { public string Name; public Bitmap Image; public int X,Y; public byte[] Pixels; }
 static void U16(BinaryWriter w,int n){w.Write((byte)(n>>8));w.Write((byte)n);}
 static void U32(BinaryWriter w,int n){w.Write((byte)(n>>24));w.Write((byte)(n>>16));w.Write((byte)(n>>8));w.Write((byte)n);}
 static void Tag(BinaryWriter w,string s){w.Write(Encoding.ASCII.GetBytes(s));}
 static byte[] Data(Bitmap b){var d=b.LockBits(new Rectangle(0,0,b.Width,b.Height),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);var a=new byte[b.Width*b.Height*4];for(int y=0;y<b.Height;y++)Marshal.Copy(d.Scan0+y*d.Stride,a,y*b.Width*4,b.Width*4);b.UnlockBits(d);return a;}
 public static Layer Make(string path,string name,float sx,float sy,int x,int y,int cropX,int cropW){
  using(var src=new Bitmap(path)) {if(cropW==0)cropW=src.Width;var im=new Bitmap((int)Math.Round(cropW*sx),(int)Math.Round(src.Height*sy),PixelFormat.Format32bppArgb);using(var g=Graphics.FromImage(im)){g.CompositingMode=CompositingMode.SourceCopy;g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.DrawImage(src,new Rectangle(0,0,im.Width,im.Height),new Rectangle(cropX,0,cropW,src.Height),GraphicsUnit.Pixel);}return new Layer{Name=name,Image=im,X=x,Y=y,Pixels=Data(im)};}
 }
 public static void Write(string path,int width,int height,Layer[] bottomToTop){
  using(var composite=new Bitmap(width,height,PixelFormat.Format32bppArgb)){
   using(var g=Graphics.FromImage(composite)){foreach(var l in bottomToTop)g.DrawImageUnscaled(l.Image,l.X,l.Y);}
   composite.Save(Path.ChangeExtension(path,"png"),ImageFormat.Png);
   using(var file=new BinaryWriter(File.Create(path))){
    Tag(file,"8BPS");U16(file,1);file.Write(new byte[6]);U16(file,4);U32(file,height);U32(file,width);U16(file,8);U16(file,3);U32(file,0);
    U32(file,28);Tag(file,"8BIM");U16(file,1005);U16(file,0);U32(file,16);U32(file,72*65536);U16(file,1);U16(file,1);U32(file,72*65536);U16(file,1);U16(file,1);
    using(var info=new MemoryStream())using(var w=new BinaryWriter(info)){
     U16(w,-bottomToTop.Length);
     for(int i=0;i<bottomToTop.Length;i++){var l=bottomToTop[i];int n=l.Image.Width*l.Image.Height;
      U32(w,l.Y);U32(w,l.X);U32(w,l.Y+l.Image.Height);U32(w,l.X+l.Image.Width);U16(w,4);
      foreach(int id in new int[]{-1,0,1,2}){U16(w,id);U32(w,n+2);}Tag(w,"8BIM");Tag(w,"norm");w.Write((byte)255);w.Write((byte)0);w.Write((byte)0);w.Write((byte)0);
      byte[] name=Encoding.ASCII.GetBytes(l.Name);int nameLen=((name.Length+1+3)/4)*4;U32(w,8+nameLen);U32(w,0);U32(w,0);w.Write((byte)name.Length);w.Write(name);w.Write(new byte[nameLen-name.Length-1]);
     }
     for(int i=0;i<bottomToTop.Length;i++){var l=bottomToTop[i];foreach(int c in new int[]{3,2,1,0}){U16(w,0);for(int p=c;p<l.Pixels.Length;p+=4)w.Write(l.Pixels[p]);}}
     if((info.Length&1)!=0)w.Write((byte)0);U32(file,(int)info.Length+8);U32(file,(int)info.Length);file.Write(info.ToArray());U32(file,0);
    }
    U16(file,0);var merged=Data(composite);foreach(int c in new int[]{2,1,0,3})for(int p=c;p<merged.Length;p+=4)file.Write(merged[p]);
   }
  }
 }
}
'@
$layers = @(
 [CubismLayerPack]::Make("$PSScriptRoot/01-RearHair.png",'Hair_Back',0.88,0.78,65,100,0,0),
 [CubismLayerPack]::Make("$PSScriptRoot/02-Body.png",'Body',0.68,0.699,45,416,0,0),
 [CubismLayerPack]::Make("$PSScriptRoot/05-Ribbons.png",'Ribbon_L',0.31,0.31,295,-5,0,750),
 [CubismLayerPack]::Make("$PSScriptRoot/05-Ribbons.png",'Ribbon_R',0.31,0.31,527,-5,750,1024),
 [CubismLayerPack]::Make("$PSScriptRoot/03-Face.png",'Face',0.347,0.347,248,164,0,0),
 [CubismLayerPack]::Make("$PSScriptRoot/04-FrontHair.png",'Hair_Front',0.37,0.37,219,161,0,0)
)
[CubismLayerPack]::Write("$PSScriptRoot/../LeafAndLeap-Character-Layers-v1.psd",1189,1323,$layers)
foreach($layer in $layers){$layer.Image.Dispose()}
