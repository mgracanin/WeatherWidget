using System.Buffers.Binary;
using System.Windows.Media.Imaging;

namespace WeatherWidget;
internal static class LegacyArtwork
{
    internal static string? RootOverride {get;set;}
    internal static string Root=>RootOverride??Path.Combine(AppSettings.DataDirectory,"LegacyArtwork");
    internal static readonly string[] Names=MakeNames();
    internal static int AvailableCount=>Names.Count(name=>File.Exists(Path.Combine(Root,name)));
    private static string[] MakeNames()
    {
        var names=new List<string>{"sun.png","partlycloudy.png","docked_blue_sun.png","docked_blue_partly-cloudy.png"};
        foreach(var prefix in new[]{"docked","undocked"})
        {
            names.Add(prefix+"-loading.png");
            foreach(var sky in new[]{"black_cloudy","black_few-showers","black_foggy","black_hail","black_moon-full","black_moon-full_partly-cloudy","black_rainy","black_snow","black_thunderstorm"})names.Add(prefix+"_"+sky+".png");
        }
        names.Add("undocked_blue_sun.png");names.Add("undocked_blue_partly-cloudy.png");return names.ToArray();
    }
    internal static int Import(string source)
    {
        source=Path.GetFullPath(source);
        var folders=new[]{source,Path.Combine(source,"images"),Path.Combine(source,"WeatherGadget"),Path.Combine(source,"WinPrognoza","slike"),Path.Combine(source,"Assets","WeatherGadget"),Path.Combine(source,"Assets","WinPrognoza","slike")};
        var accepted=new Dictionary<string,byte[]>(StringComparer.OrdinalIgnoreCase);
        foreach(var name in Names)
        {
            var found=folders.Select(folder=>Path.Combine(folder,name)).FirstOrDefault(File.Exists);
            if(found==null)continue;
            if(new FileInfo(found).Length>1024*1024)throw new InvalidDataException("Image exceeds 1 MiB: "+name);
            var bytes=ReadBounded(found);Decode(bytes);accepted[name]=bytes;
        }
        if(accepted.Count==0)throw new InvalidDataException("No supported Weather.Gadget PNG images were found in this folder.");
        Directory.CreateDirectory(Root);
        if((File.GetAttributes(Root)&FileAttributes.ReparsePoint)!=0)throw new IOException("The local artwork folder must not be a link.");
        foreach(var pair in accepted)
        {
            var destination=Path.Combine(Root,pair.Key);var temporary=destination+".import-"+Guid.NewGuid().ToString("N");
            try{File.WriteAllBytes(temporary,pair.Value);File.Move(temporary,destination,true);}
            finally{if(File.Exists(temporary))File.Delete(temporary);}
        }
        WeatherArt.ReloadLegacy();return accepted.Count;
    }
    internal static BitmapImage? Load(string name)
    {
        try
        {
            var file=Path.Combine(Root,name);
            if(!File.Exists(file)||new FileInfo(file).Length>1024*1024)return null;
            return Decode(ReadBounded(file));
        }
        catch(Exception ex) when(ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or FormatException or InvalidOperationException or NotSupportedException or System.Runtime.InteropServices.COMException){return null;}
    }
    private static byte[] ReadBounded(string file)
    {
        using var stream=File.OpenRead(file);
        long length=stream.Length;
        if(length>1024*1024)throw new InvalidDataException("Image exceeds 1 MiB.");
        var bytes=new byte[(int)length];stream.ReadExactly(bytes);return bytes;
    }
    private static BitmapImage Decode(byte[] bytes)
    {
        if(bytes.Length<24||!bytes.AsSpan(0,8).SequenceEqual(new byte[]{137,80,78,71,13,10,26,10})||!bytes.AsSpan(12,4).SequenceEqual("IHDR"u8))throw new InvalidDataException("Not a PNG image.");
        uint width=BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(16,4)),height=BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(20,4));
        if(width==0||height==0||width>1024||height>1024||width*height>524288)throw new InvalidDataException("PNG dimensions exceed the local theme limit.");
        using var stream=new MemoryStream(bytes,false);
        var image=new BitmapImage();image.BeginInit();image.CacheOption=BitmapCacheOption.OnLoad;image.StreamSource=stream;image.EndInit();image.Freeze();return image;
    }
}
