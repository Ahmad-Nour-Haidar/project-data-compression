// File: ShannonFanoCompressor.cs 

using System.Diagnostics;
using ProjectDataCompression.Models;

namespace ProjectDataCompression.Algorithms;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

public class ShannonFanoCompressor
{
    private CancellationTokenSource _cts;
    private ManualResetEventSlim _pauseEvent = new(true);

    public event Action<int> ProgressChanged;
    public event Action<string> StatusChanged;

    private Dictionary<byte, string> _codes;
    private Dictionary<string, byte> _reverseCodes;

    private void BuildShannonFanoCodes(List<(byte symbol, int freq)> symbols, int start, int end, string code)
    {
        if (start == end)
        {
            _codes[symbols[start].symbol] = code;
            _reverseCodes[code] = symbols[start].symbol;
            return;
        }

        // Total is the sum of current sub list
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

    public async Task<CompressionResult> CompressAsync(string inputPath, string outputPath)
    {
        _cts = new CancellationTokenSource();
        StatusChanged?.Invoke("Starting Shannon-Fano Compression...");

        var stopwatch = Stopwatch.StartNew();

        await Task.Run(() =>
        {
            var token = _cts.Token;
            byte[] inputData = File.ReadAllBytes(inputPath);

            var frequencies = inputData.GroupBy(b => b).ToDictionary(g => g.Key, g => g.Count());
            var sortedSymbols = frequencies.Select(kv => (kv.Key, kv.Value)).OrderByDescending(x => x.Value).ToList();

            _codes = new();
            _reverseCodes = new();
            BuildShannonFanoCodes(sortedSymbols, 0, sortedSymbols.Count - 1, "");

            using var output = new BinaryWriter(File.Create(outputPath));

            output.Write(frequencies.Count);
            foreach (var kvp in frequencies)
            {
                output.Write(kvp.Key);
                output.Write(kvp.Value);
            }

            string bitString = string.Join("", inputData.Select(b => _codes[b]));
            List<byte> compressedBytes = new();
            for (int i = 0; i < bitString.Length; i += 8)
            {
                token.ThrowIfCancellationRequested();
                _pauseEvent.Wait();

                string chunk = bitString.Substring(i, Math.Min(8, bitString.Length - i)).PadRight(8, '0');
                compressedBytes.Add(Convert.ToByte(chunk, 2));

                var progress = (i * 100 / bitString.Length);
                ProgressChanged?.Invoke(progress);
            }

            output.Write(compressedBytes.ToArray());
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


    public async Task DecompressAsync(string inputPath, string outputPath)
    {
        _cts = new CancellationTokenSource();
        StatusChanged?.Invoke("Starting Shannon-Fano Decompression...");

        await Task.Run(() =>
        {
            var token = _cts.Token;
            using var input = new BinaryReader(File.OpenRead(inputPath));

            int freqCount = input.ReadInt32();
            var frequencies = new Dictionary<byte, int>();
            for (int i = 0; i < freqCount; i++)
            {
                byte b = input.ReadByte();
                int f = input.ReadInt32();
                frequencies[b] = f;
            }

            var sortedSymbols = frequencies.Select(kv => (kv.Key, kv.Value)).OrderByDescending(x => x.Value).ToList();

            _codes = new();
            _reverseCodes = new();
            BuildShannonFanoCodes(sortedSymbols, 0, sortedSymbols.Count - 1, "");

            List<byte> compressed = new();
            while (input.BaseStream.Position < input.BaseStream.Length)
            {
                compressed.Add(input.ReadByte());
            }


            string bitString = string.Join("", compressed.Select(b => Convert.ToString(b, 2).PadLeft(8, '0')));
            List<byte> outputData = new();
            string current = "";

            for (int i = 0; i < bitString.Length; i++)
            {
                token.ThrowIfCancellationRequested();
                _pauseEvent.Wait();

                current += bitString[i];
                if (_reverseCodes.TryGetValue(current, out byte b))
                {
                    outputData.Add(b);
                    current = "";
                }

                var progress = (i * 100 / bitString.Length);
                ProgressChanged?.Invoke(progress);
            }

            File.WriteAllBytes(outputPath, outputData.ToArray());
            StatusChanged?.Invoke("Shannon-Fano Decompression Complete.");
        }, _cts.Token);
    }

    public void Pause() => _pauseEvent.Reset();
    public void Resume() => _pauseEvent.Set();
    public void Cancel() => _cts.Cancel();
}