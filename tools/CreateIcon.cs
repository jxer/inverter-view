// Convert the supplied artwork into a Windows ICO without changing its design.
// Compile with csc /r:System.Drawing.dll tools\CreateIcon.cs, then pass PNG and ICO paths.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

static class CreateIcon
{
    static int Main(string[] args)
    {
        if(args.Length!=2){Console.Error.WriteLine("Usage: CreateIcon.exe source.png output.ico");return 1;}
        int[] sizes={16,20,24,32,40,48,64,128,256};var frames=new List<byte[]>();
        using(var source=Image.FromFile(args[0]))
        foreach(int size in sizes)
        using(var bitmap=new Bitmap(size,size,PixelFormat.Format32bppArgb))
        {
            using(var graphics=Graphics.FromImage(bitmap))
            {
                graphics.Clear(Color.Transparent);graphics.InterpolationMode=InterpolationMode.HighQualityBicubic;graphics.PixelOffsetMode=PixelOffsetMode.HighQuality;
                float scale=Math.Min((float)size/source.Width,(float)size/source.Height);float w=source.Width*scale,h=source.Height*scale;
                graphics.DrawImage(source,(size-w)/2,(size-h)/2,w,h);
            }
            using(var stream=new MemoryStream()){bitmap.Save(stream,ImageFormat.Png);frames.Add(stream.ToArray());}
        }
        using(var output=new BinaryWriter(File.Create(args[1])))
        {
            output.Write((ushort)0);output.Write((ushort)1);output.Write((ushort)sizes.Length);int offset=6+16*sizes.Length;
            for(int i=0;i<sizes.Length;i++)
            {
                output.Write((byte)(sizes[i]==256?0:sizes[i]));output.Write((byte)(sizes[i]==256?0:sizes[i]));output.Write((byte)0);output.Write((byte)0);
                output.Write((ushort)1);output.Write((ushort)32);output.Write(frames[i].Length);output.Write(offset);offset+=frames[i].Length;
            }
            foreach(byte[] frame in frames)output.Write(frame);
        }
        Console.WriteLine("Created nine icon sizes from the supplied artwork.");return 0;
    }
}
