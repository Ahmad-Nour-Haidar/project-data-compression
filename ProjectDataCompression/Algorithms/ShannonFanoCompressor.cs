// File: ShannonFanoCompressor.cs

using ProjectDataCompression.Models;
using System.Diagnostics;
using System.Text;
using ProjectDataCompression.Enums;
using ProjectDataCompression.Functions;

namespace ProjectDataCompression.Algorithms;

public class ShannonFanoCompressor
{
    private Dictionary<byte, string> _codes;
    private Dictionary<string, byte> _reverseCodes;
    private CancellationTokenSource _cts;
    private ManualResetEventSlim _pauseEvent = new(true);

    public event Action<int> ProgressChanged;
    public event Action<string> StatusChanged;

    private void BuildShannonFanoCodes(List<(byte symbol, int freq)> symbols, int start, int end, string code)
    {
        if (start == end)
        {
            _codes[symbols[start].symbol] = code;
            _reverseCodes[code] = symbols[start].symbol;
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

        BuildShannonFanoCodes(symbols, start, split, code + "0");
        BuildShannonFanoCodes(symbols, split + 1, end, code + "1");
    }

    public async Task<CompressionResult> CompressAsync(string inputPath, string outputPath, string? password)
    {
        _cts = new CancellationTokenSource();
        _codes = new();
        _reverseCodes = new();
        StatusChanged?.Invoke("Starting Shannon-Fano Compression...");

        var stopwatch = Stopwatch.StartNew();

        await Task.Run(() =>
        {
            var token = _cts.Token;
            byte[] inputData = File.ReadAllBytes(inputPath);

            var frequencies = inputData.GroupBy(b => b).ToDictionary(g => g.Key, g => g.Count());
            var sortedFrequencies =
                frequencies.Select(kv => (kv.Key, kv.Value)).OrderByDescending(x => x.Value).ToList();

            BuildShannonFanoCodes(sortedFrequencies, 0, sortedFrequencies.Count - 1, "");

            using var output = new BinaryWriter(File.Create(outputPath));

            // --- Save Metadata ---
            string algorithm = nameof(CompressorType.ShannonFano);
            string originalExtension = Path.GetExtension(inputPath);
            output.Write(algorithm);
            output.Write(originalExtension);
            
            // --- Write Password Hash (or empty string) ---
            string passwordHash = string.IsNullOrWhiteSpace(password) ? "" : ComputeSha256Hash.Make(password);
            output.Write(passwordHash);

            // --- Frequency Table ---
            output.Write(frequencies.Count);
            foreach (var kvp in frequencies)
            {
                output.Write(kvp.Key);
                output.Write(kvp.Value);
            }

            // --- Encode ---
            string bitString = string.Join("", inputData.Select(b => _codes[b]));
            List<byte> encoded = new();
            for (int i = 0; i < bitString.Length; i += 8)
            {
                token.ThrowIfCancellationRequested();
                _pauseEvent.Wait();

                string chunk = bitString.Substring(i, Math.Min(8, bitString.Length - i)).PadRight(8, '0');
                encoded.Add(Convert.ToByte(chunk, 2));

                int progress = i * 100 / bitString.Length;
                ProgressChanged?.Invoke(progress);
            }

            output.Write(encoded.ToArray());
            output.Write(bitString.Length);

            StatusChanged?.Invoke("Shannon-Fano Compression Complete.");
            ProgressChanged?.Invoke(100);
        }, _cts.Token);

        stopwatch.Stop();

        return new CompressionResult
        {
            OriginalSize = new FileInfo(inputPath).Length,
            CompressedSize = new FileInfo(outputPath).Length,
            Duration = stopwatch.Elapsed
        };
    }

    public async Task<string> DecompressAsync(string inputPath)
    {
        _cts = new CancellationTokenSource();
        StatusChanged?.Invoke("Starting Shannon-Fano Decompression...");

        return await Task.Run(() =>
        {
            var token = _cts.Token;
            using var input = new BinaryReader(File.OpenRead(inputPath));

            // --- Read Metadata ---
            string algorithm = input.ReadString();
            string originalExtension = input.ReadString();
            
            string savedPasswordHash = input.ReadString();

            // --- Request Password if Needed ---
            if (!string.IsNullOrWhiteSpace(savedPasswordHash))
            {
                String enteredPassword = PasswordDialog.RequestPassword("Please enter your password:");
                if (string.IsNullOrWhiteSpace(enteredPassword))
                {
                    throw new UnauthorizedAccessException("Password is required to decompress this file.");
                    // MessageBox.Show("Password is required to decompress this file.", "Password required");
                    // return "";
                }

                string enteredHash = ComputeSha256Hash.Make(enteredPassword);
                if (enteredHash != savedPasswordHash)
                {
                    throw new UnauthorizedAccessException("Incorrect password.");
                    // MessageBox.Show("Incorrect password.", "Incorrect password.");
                }
            }

            // --- Read Frequency Table ---
            int count = input.ReadInt32();
            var freqList = new List<(byte, int)>();
            for (int i = 0; i < count; i++)
            {
                byte b = input.ReadByte();
                int f = input.ReadInt32();
                freqList.Add((b, f));
            }

            _codes = new();
            _reverseCodes = new();
            BuildShannonFanoCodes(freqList.OrderByDescending(f => f.Item2).ToList(), 0, freqList.Count - 1, "");

            List<byte> encoded = new();
            while (input.BaseStream.Position < input.BaseStream.Length - sizeof(int))
            {
                encoded.Add(input.ReadByte());
            }

            int bitLength = input.ReadInt32();
            string bitString = string.Join("", encoded.Select(b => Convert.ToString(b, 2).PadLeft(8, '0')));
            bitString = bitString.Substring(0, bitLength);

            List<byte> outputData = new();
            StringBuilder currentCode = new();

            for (int i = 0; i < bitString.Length; i++)
            {
                token.ThrowIfCancellationRequested();
                _pauseEvent.Wait();

                currentCode.Append(bitString[i]);
                if (_reverseCodes.TryGetValue(currentCode.ToString(), out byte symbol))
                {
                    outputData.Add(symbol);
                    currentCode.Clear();
                }

                int progress = i * 100 / bitString.Length;
                ProgressChanged?.Invoke(progress);
            }

            string decompressedPath = Path.Combine(
                Path.GetDirectoryName(inputPath)!,
                Path.GetFileNameWithoutExtension(inputPath) + "_decompressed" + originalExtension
            );

            File.WriteAllBytes(decompressedPath, outputData.ToArray());
            StatusChanged?.Invoke("Shannon-Fano Decompression Complete.");
            ProgressChanged?.Invoke(100);

            return decompressedPath;
        }, _cts.Token);
    }

    public void Pause() => _pauseEvent.Reset();
    public void Resume() => _pauseEvent.Set();
    public void Cancel() => _cts?.Cancel();
}