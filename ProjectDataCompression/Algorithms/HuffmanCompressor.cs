// File: HuffmanCompressor.cs 

using System.Diagnostics;
using ProjectDataCompression.Enums;
using ProjectDataCompression.Functions;
using ProjectDataCompression.Models;

namespace ProjectDataCompression.Algorithms;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

public class HuffmanCompressor
{
    private class Node
    {
        public byte Symbol;
        public int Frequency;
        public Node Left, Right;

        public bool IsLeaf => Left == null && Right == null;
    }

    private Dictionary<byte, string> _codes;
    private CancellationTokenSource _cts;
    private ManualResetEventSlim _pauseEvent = new(true);

    public event Action<int> ProgressChanged;
    public event Action<string> StatusChanged;

    public Func<string?>? RequestPassword;

    private Node BuildTree(Dictionary<byte, int> frequencies)
    {
        var pq = new PriorityQueue<Node, int>();
        foreach (var kvp in frequencies)
            pq.Enqueue(new Node { Symbol = kvp.Key, Frequency = kvp.Value }, kvp.Value);

        while (pq.Count > 1)
        {
            var left = pq.Dequeue();
            var right = pq.Dequeue();
            var parent = new Node { Left = left, Right = right, Frequency = left.Frequency + right.Frequency };
            pq.Enqueue(parent, parent.Frequency);
        }

        return pq.Dequeue();
    }

    private void BuildCodes(Node? node, string code)
    {
        if (node == null) return;
        if (node.IsLeaf)
        {
            _codes[node.Symbol] = code;
        }
        else
        {
            BuildCodes(node.Left, code + "0");
            BuildCodes(node.Right, code + "1");
        }
    }

    public async Task<CompressionResult> CompressAsync(string inputPath, string outputPath, string? password)
    {
        _cts = new CancellationTokenSource();
        _codes = new();
        StatusChanged?.Invoke("Starting Huffman Compression...");

        var stopwatch = Stopwatch.StartNew();

        await Task.Run(() =>
        {
            var token = _cts.Token;
            byte[] inputData = File.ReadAllBytes(inputPath);
            var frequencies = inputData.GroupBy(b => b).ToDictionary(g => g.Key, g => g.Count());
            Node root = BuildTree(frequencies);
            BuildCodes(root, "");

            using var output = new BinaryWriter(File.Create(outputPath));

            // --- Save Metadata ---
            string algorithm = nameof(CompressorType.Huffman);
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

            // --- Compress Data ---
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

            StatusChanged?.Invoke("Huffman Compression Complete.");
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
        StatusChanged?.Invoke("Starting Huffman Decompression...");

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
            int freqCount = input.ReadInt32();
            var frequencies = new Dictionary<byte, int>();
            for (int i = 0; i < freqCount; i++)
            {
                byte b = input.ReadByte();
                int f = input.ReadInt32();
                frequencies[b] = f;
            }

            Node root = BuildTree(frequencies);
            List<byte> compressed = new();
            while (input.BaseStream.Position < input.BaseStream.Length - sizeof(int))
            {
                compressed.Add(input.ReadByte());
            }

            int bitLength = input.ReadInt32();
            string bitString = string.Join("", compressed.Select(b => Convert.ToString(b, 2).PadLeft(8, '0')));
            bitString = bitString.Substring(0, bitLength);

            List<byte> outputData = new();
            Node current = root;

            for (int i = 0; i < bitString.Length; i++)
            {
                token.ThrowIfCancellationRequested();
                _pauseEvent.Wait();

                current = bitString[i] == '0' ? current.Left : current.Right;
                if (current.IsLeaf)
                {
                    outputData.Add(current.Symbol);
                    current = root;
                }

                var progress = (i * 100 / bitString.Length);
                ProgressChanged?.Invoke(progress);
            }

            string decompressedPath = Path.Combine(
                Path.GetDirectoryName(inputPath)!,
                Path.GetFileNameWithoutExtension(inputPath) + "_decompressed" + originalExtension
            );

            File.WriteAllBytes(decompressedPath, outputData.ToArray());
            StatusChanged?.Invoke("Huffman Decompression Complete.");
            ProgressChanged?.Invoke(100);

            return decompressedPath;
        }, _cts.Token);
    }

    public void Pause() => _pauseEvent.Reset();
    public void Resume() => _pauseEvent.Set();
    public void Cancel() => _cts?.Cancel();
}