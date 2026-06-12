namespace MMS.Server.Services.Msi
{
    public static class Crc32
    {
        private static readonly uint[] Table = CreateTable();

        private static uint[] CreateTable()
        {
            uint[] table = new uint[256];

            for (uint i = 0; i < table.Length; i++)
            {
                uint crc = i;

                for (int j = 0; j < 8; j++)
                {
                    crc = (crc & 1) == 1
                        ? 0xEDB88320 ^ (crc >> 1)
                        : crc >> 1;
                }

                table[i] = crc;
            }

            return table;
        }

        public static uint Compute(byte[] bytes)
        {
            uint crc = 0xFFFFFFFF;

            foreach (byte b in bytes)
            {
                byte index = (byte)((crc & 0xFF) ^ b);
                crc = (crc >> 8) ^ Table[index];
            }

            return ~crc;
        }
    }
}