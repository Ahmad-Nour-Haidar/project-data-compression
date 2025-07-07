namespace ProjectDataCompression.Project;

public class ShannonFanoCompressor
{
    private readonly ManualResetEventSlim _pauseEvent = new(true);
    private CancellationTokenSource _cts;

    public event Action<int> ProgressChanged;

    private void BuildShannonFanoCodes(List<(byte symbol, int freq)> symbols, int start, int end, string code,
        Dictionary<byte, string> codes)
    {
        if (start == end)
        {
            codes[symbols[start].symbol] = string.IsNullOrEmpty(code) ? "0" : code;
            return;
        }

        int total = symbols.Skip(start).Take(end - start + 1).Sum(x => x.freq);
        int split = start, leftSum = 0;

        for (int i = start; i <= end; i++)
        {
            leftSum += symbols[i].freq;
            if (leftSum >= total / 2)
            {
                split = i;
                break;
            }
        }

        BuildShannonFanoCodes(symbols, start, split, code + "0", codes);
        BuildShannonFanoCodes(symbols, split + 1, end, code + "1", codes);
    }

    public async Task<byte[]> CompressWithShannonFano(byte[] data, Dictionary<byte, int> frequencies)
    {
        _cts = new CancellationTokenSource();

        return await Task.Run(() =>
        {
            var token = _cts.Token;
            var codes = new Dictionary<byte, string>();
            var sortedFrequencies = frequencies.Select(kv => (kv.Key, kv.Value))
                .OrderByDescending(x => x.Value)
                .ToList();

            BuildShannonFanoCodes(sortedFrequencies, 0, sortedFrequencies.Count - 1, "", codes);

            string bitString = string.Join("", data.Select(b => codes[b]));

            var result = new List<byte>();
            for (int i = 0; i < bitString.Length; i += 8)
            {
                token.ThrowIfCancellationRequested();
                _pauseEvent.Wait();
                string chunk = bitString.Substring(i, Math.Min(8, bitString.Length - i)).PadRight(8, '0');
                result.Add(Convert.ToByte(chunk, 2));

                if (i % 1000 == 0)
                {
                    int progress = i * 100 / bitString.Length;
                    ProgressChanged?.Invoke(progress);
                }
            }

            result.AddRange(BitConverter.GetBytes(bitString.Length));
            ProgressChanged?.Invoke(100);
            return result.ToArray();
        }, _cts.Token);
    }

    public async Task<byte[]> DecompressWithShannonFano(byte[] compressedData, Dictionary<byte, int> frequencies)
    {
        _cts = new CancellationTokenSource();
        ProgressChanged?.Invoke(0);
        return await Task.Run(() =>
        {
            var token = _cts.Token;
            var codes = new Dictionary<byte, string>();
            var reverseCodes = new Dictionary<string, byte>();

            var sortedFrequencies = frequencies.Select(kv => (kv.Key, kv.Value))
                .OrderByDescending(x => x.Value)
                .ToList();

            BuildShannonFanoCodes(sortedFrequencies, 0, sortedFrequencies.Count - 1, "", codes);

            foreach (var code in codes)
            {
                reverseCodes[code.Value] = code.Key;
            }

            int bitLength = BitConverter.ToInt32(compressedData, compressedData.Length - 4);

            string bitString = string.Join("",
                compressedData.Take(compressedData.Length - 4)
                    .Select(b => Convert.ToString(b, 2).PadLeft(8, '0')));
            bitString = bitString.Substring(0, bitLength);

            var result = new List<byte>();
            string currentCode = "";

            for (int i = 0; i < bitString.Length; i++)
            {
                token.ThrowIfCancellationRequested();
                _pauseEvent.Wait();
                currentCode += bitString[i];
                if (reverseCodes.TryGetValue(currentCode, out byte symbol))
                {
                    result.Add(symbol);
                    currentCode = "";
                }

                if (i % 1000 == 0)
                {
                    int progress = i * 100 / bitString.Length;
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