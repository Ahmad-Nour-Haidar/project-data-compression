using ProjectDataCompression.Project;

namespace ProjectDataCompression.Algorithms;

public class HuffmanCompressor
{
    private readonly ManualResetEventSlim _pauseEvent = new(true);
    private CancellationTokenSource _cts;
    public event Action<int> ProgressChanged;

    private Node BuildHuffmanTree(Dictionary<byte, int> frequencies)
    {
        var pq = new PriorityQueue<Node, int>();
        foreach (var kvp in frequencies)
        {
            pq.Enqueue(new Node { Symbol = kvp.Key, Frequency = kvp.Value }, kvp.Value);
        }

        while (pq.Count > 1)
        {
            var left = pq.Dequeue();
            var right = pq.Dequeue();
            var parent = new Node { Left = left, Right = right, Frequency = left.Frequency + right.Frequency };
            pq.Enqueue(parent, parent.Frequency);
        }

        return pq.Dequeue();
    }

    private void BuildHuffmanCodes(Node? node, string code, Dictionary<byte, string> codes)
    {
        if (node == null) return;

        if (node.IsLeaf)
        {
            codes[node.Symbol] = string.IsNullOrEmpty(code) ? "0" : code;
        }
        else
        {
            BuildHuffmanCodes(node.Left, code + "0", codes);
            BuildHuffmanCodes(node.Right, code + "1", codes);
        }
    }

    public async Task<byte[]> CompressWithHuffman(byte[] data, Dictionary<byte, int> frequencies)
    {
        ProgressChanged?.Invoke(0);
        _cts = new CancellationTokenSource();
        return await Task.Run(() =>
        {
            var token = _cts.Token;
            var root = BuildHuffmanTree(frequencies);
            var codes = new Dictionary<byte, string>();
            BuildHuffmanCodes(root, "", codes);

            string bitString = string.Join("", data.Select(b => codes[b]));

            var result = new List<byte>();

            for (int i = 0; i < bitString.Length; i += 8)
            {
                token.ThrowIfCancellationRequested();
                _pauseEvent.Wait();


                string chunk = bitString.Substring(i, Math.Min(8, bitString.Length - i)).PadRight(8, '0');
                result.Add(Convert.ToByte(chunk, 2));
                
                // // to test pause and resume
                // if (i % 100 == 0)
                // {
                //     Thread.Sleep(1);
                // }

                if (i % 1000 == 0)
                {
                    var progress = (i * 100 / bitString.Length);
                    ProgressChanged?.Invoke(progress);
                }
            }

            result.AddRange(BitConverter.GetBytes(bitString.Length));
            ProgressChanged?.Invoke(100);
            return result.ToArray();
        }, _cts.Token);
    }

    public async Task<byte[]> DecompressWithHuffman(byte[] compressedData, Dictionary<byte, int> frequencies)
    {
        _cts = new CancellationTokenSource();
        ProgressChanged?.Invoke(0);

        return await Task.Run(() =>
        {
            var token = _cts.Token;
            var root = BuildHuffmanTree(frequencies);

            int bitLength = BitConverter.ToInt32(compressedData, compressedData.Length - 4);

            string bitString = string.Join("",
                compressedData.Take(compressedData.Length - 4)
                    .Select(b => Convert.ToString(b, 2).PadLeft(8, '0')));
            bitString = bitString.Substring(0, bitLength);

            var result = new List<byte>();
            Node current = root;

            for (int i = 0; i < bitString.Length; i++)
            {
                token.ThrowIfCancellationRequested();
                _pauseEvent.Wait();
                current = bitString[i] == '0' ? current.Left : current.Right;
                if (current != null && current.IsLeaf)
                {
                    result.Add(current.Symbol);
                    current = root;
                }

                if (i % 1000 == 0)
                {
                    var progress = (i * 100 / bitString.Length);
                    ProgressChanged?.Invoke(progress);
                }
            }

            ProgressChanged?.Invoke(100);
            return result.ToArray();
        }, _cts.Token);
    }

    public void Pause() => _pauseEvent.Reset();
    public void Resume() => _pauseEvent.Set();

    public void Cancel() => _cts?.Cancel();
}