namespace MMS.Server.Services.Msi
{
    public class MsiHeader
    {
        public string Magic { get; set; } = "MSI0";
        public ushort Version { get; set; } = 1;
        public ushort HeaderLength { get; set; } = 28;
        public uint Width { get; set; }
        public uint Height { get; set; }
        public byte Channels { get; set; } = 3;
        public byte ColorSpace { get; set; } = 3;
        public byte Compression { get; set; } = 0;
        public byte Reserved { get; set; } = 0;
        public uint MetaLength { get; set; }
        public uint PixelLength { get; set; }
    }
}