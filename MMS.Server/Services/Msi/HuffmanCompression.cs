namespace MMS.Server.Services.Msi
{
    public static class HuffmanCompression
    {
        private class Node
        {
            public byte? Symbol { get; set; }
            public int Frequency { get; set; }
            public Node? Left { get; set; }
            public Node? Right { get; set; }

            public bool IsLeaf => Left == null && Right == null;
        }

        public static byte[] Compress(byte[] data, out Dictionary<byte, string> codes, out int bitLength)
        {
            codes = new Dictionary<byte, string>();
            bitLength = 0;

            if (data.Length == 0)
                return Array.Empty<byte>();

            var frequencies = data
                .GroupBy(b => b)
                .ToDictionary(g => g.Key, g => g.Count());

            Node root = BuildTree(frequencies);
            BuildCodes(root, "", codes);

            List<bool> bits = new();

            foreach (byte b in data)
            {
                string code = codes[b];

                foreach (char c in code)
                    bits.Add(c == '1');
            }

            bitLength = bits.Count;
            return BitsToBytes(bits);
        }

        public static byte[] Decompress(
            byte[] compressedData,
            Dictionary<byte, string> codes,
            int bitLength,
            int expectedLength)
        {
            if (compressedData.Length == 0)
                return Array.Empty<byte>();

            Dictionary<string, byte> reverseCodes = codes.ToDictionary(x => x.Value, x => x.Key);

            List<byte> output = new();
            string current = "";

            int bitsRead = 0;

            foreach (byte b in compressedData)
            {
                for (int i = 7; i >= 0; i--)
                {
                    if (bitsRead >= bitLength)
                        break;

                    bool bit = ((b >> i) & 1) == 1;
                    current += bit ? "1" : "0";
                    bitsRead++;

                    if (reverseCodes.TryGetValue(current, out byte symbol))
                    {
                        output.Add(symbol);
                        current = "";

                        if (output.Count == expectedLength)
                            return output.ToArray();
                    }
                }
            }

            return output.ToArray();
        }

        private static Node BuildTree(Dictionary<byte, int> frequencies)
        {
            List<Node> nodes = frequencies
                .Select(f => new Node
                {
                    Symbol = f.Key,
                    Frequency = f.Value
                })
                .ToList();

            if (nodes.Count == 1)
            {
                return new Node
                {
                    Frequency = nodes[0].Frequency,
                    Left = nodes[0],
                    Right = null
                };
            }

            while (nodes.Count > 1)
            {
                nodes = nodes
                    .OrderBy(n => n.Frequency)
                    .ToList();

                Node left = nodes[0];
                Node right = nodes[1];

                Node parent = new()
                {
                    Frequency = left.Frequency + right.Frequency,
                    Left = left,
                    Right = right
                };

                nodes.Remove(left);
                nodes.Remove(right);
                nodes.Add(parent);
            }

            return nodes[0];
        }

        private static void BuildCodes(Node node, string code, Dictionary<byte, string> codes)
        {
            if (node.IsLeaf && node.Symbol.HasValue)
            {
                codes[node.Symbol.Value] = string.IsNullOrEmpty(code) ? "0" : code;
                return;
            }

            if (node.Left != null)
                BuildCodes(node.Left, code + "0", codes);

            if (node.Right != null)
                BuildCodes(node.Right, code + "1", codes);
        }

        private static byte[] BitsToBytes(List<bool> bits)
        {
            int byteCount = (bits.Count + 7) / 8;
            byte[] result = new byte[byteCount];

            for (int i = 0; i < bits.Count; i++)
            {
                if (bits[i])
                    result[i / 8] |= (byte)(1 << (7 - (i % 8)));
            }

            return result;
        }
    }
}