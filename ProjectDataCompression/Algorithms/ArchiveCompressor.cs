using System.Diagnostics;
using ProjectDataCompression.Enums;
using ProjectDataCompression.Functions;
using ProjectDataCompression.Models;
using ProjectDataCompression.Project;

namespace ProjectDataCompression.Algorithms;

public class ArchiveCompressor
{
    public static readonly String CompressedFileExt = "compress";

    private HuffmanCompressor _huffmanCompressor = new();
    private ShannonFanoCompressor _shannonFanoCompressor = new();

    public event Action<int> ProgressChangedHuffman;
    public event Action<int> ProgressChangedShannonFano;

    public async Task<CompressionResult> CompressMultipleFilesAsync(
        string[] inputPaths,
        string outputPath,
        string? password = null,
        CompressorType algorithm = CompressorType.Huffman)
    {
        var stopwatch = Stopwatch.StartNew();
        long totalOriginalSize = 0;
        int totalFiles = inputPaths.Length;

        var metadata = new ArchiveMetadata
        {
            Algorithm = algorithm.ToString(),
            PasswordHash = string.IsNullOrWhiteSpace(password) ? "" : ComputeSha256Hash.Make(password)
        };

        using var output = new BinaryWriter(File.Create(outputPath));
        output.Write(0L); // Reserve space for metadata pointer

        for (int i = 0; i < totalFiles; i++)
        {
            string inputPath = inputPaths[i];
            if (!File.Exists(inputPath)) continue;


            var fileInfo = new FileInfo(inputPath);
            totalOriginalSize += fileInfo.Length;

            var entry = new ArchiveEntry
            {
                FileName = Path.GetFileName(inputPath),
                RelativePath = inputPath,
                OriginalSize = fileInfo.Length,
                DataOffset = output.BaseStream.Position
            };

            byte[] fileData = File.ReadAllBytes(inputPath);
            var frequencies = fileData.GroupBy(b => b).ToDictionary(g => g.Key, g => g.Count());
            entry.Frequencies = frequencies;

            byte[] compressedData;

            if (algorithm == CompressorType.Huffman)
            {
                _huffmanCompressor.ProgressChanged += p =>
                {
                    double perFileWeight = 100.0 / totalFiles;
                    double progress = (p / 100.0) * perFileWeight + perFileWeight * i;
                    ProgressChangedHuffman?.Invoke(Math.Clamp((int)Math.Round(progress), 0, 100));
                };
                compressedData = await _huffmanCompressor.CompressWithHuffman(fileData, frequencies);
                _huffmanCompressor.ProgressChanged -= ProgressChangedHuffman;
            }
            else if (algorithm == CompressorType.ShannonFano)
            {
                _shannonFanoCompressor.ProgressChanged += p =>
                {
                    double perFileWeight = 100.0 / totalFiles;
                    double progress = (p / 100.0) * perFileWeight + perFileWeight * i;
                    ProgressChangedShannonFano?.Invoke(Math.Clamp((int)Math.Round(progress), 0, 100));
                };
                compressedData = await _shannonFanoCompressor.CompressWithShannonFano(fileData, frequencies);
                _shannonFanoCompressor.ProgressChanged -= ProgressChangedShannonFano;
            }
            else
            {
                throw new NotSupportedException($"Algorithm {algorithm} is not supported.");
            }

            entry.CompressedSize = compressedData.Length;
            entry.CompressedDataLength = compressedData.Length;

            output.Write(compressedData);
            metadata.Entries.Add(entry);
        }

        long metadataPosition = output.BaseStream.Position;
        WriteMetadata(output, metadata);
        output.BaseStream.Seek(0, SeekOrigin.Begin);
        output.Write(metadataPosition);

        stopwatch.Stop();

        if (algorithm == CompressorType.Huffman)
        {
            ProgressChangedHuffman?.Invoke(100);
        }
        else if (algorithm == CompressorType.ShannonFano)
        {
            ProgressChangedShannonFano?.Invoke(100);
        }

        return new CompressionResult
        {
            OriginalSize = totalOriginalSize,
            CompressedSize = new FileInfo(outputPath).Length,
            Duration = stopwatch.Elapsed
        };
    }

    public async Task<string> ExtractSingleFileAsync(string archivePath, string fileName, string? outputDirectory)
    {
        return await Task.Run(async () =>
        {
            using var input = new BinaryReader(File.OpenRead(archivePath));

            // read metadata position
            long metadataPosition = input.ReadInt64();

            // read metadata
            input.BaseStream.Seek(metadataPosition, SeekOrigin.Begin);
            var metadata = ReadMetadata(input);

            // validate if there a password
            if (!string.IsNullOrWhiteSpace(metadata.PasswordHash))
            {
                string enteredPassword = PasswordDialog.RequestPassword("Please enter archive password:");
                if (string.IsNullOrWhiteSpace(enteredPassword))
                {
                    throw new UnauthorizedAccessException("Password is required to extract files.");
                }

                string enteredHash = ComputeSha256Hash.Make(enteredPassword);
                if (enteredHash != metadata.PasswordHash)
                {
                    throw new UnauthorizedAccessException("Incorrect password.");
                }
            }

            // search for target file
            var targetEntry = metadata.Entries.FirstOrDefault(e =>
                e.FileName.Equals(fileName, StringComparison.OrdinalIgnoreCase));

            if (targetEntry == null)
            {
                throw new FileNotFoundException($"File '{fileName}' not found in archive.");
            }


            input.BaseStream.Seek(targetEntry.DataOffset, SeekOrigin.Begin);
            byte[] compressedData = input.ReadBytes((int)targetEntry.CompressedDataLength);

            var algorithm = Enum.Parse<CompressorType>(metadata.Algorithm);
            byte[] decompressedData =
                await DecompressFileData(compressedData, targetEntry.Frequencies, algorithm, 0, 1);

            string outputDir = outputDirectory ?? Path.GetDirectoryName(archivePath)!;
            string outputPath = Path.Combine(outputDir,
                $"{Path.GetFileNameWithoutExtension(targetEntry.FileName)}_decompressed{Path.GetExtension(targetEntry.FileName)}");

            File.WriteAllBytes(outputPath, decompressedData);


            return outputPath;
        });
    }

    public async Task<string[]> ExtractAllFilesAsync(string archivePath, string? outputDirectory = null)
    {
        return await Task.Run(async () =>
        {
            var extractedFiles = new List<string>();

            using var input = new BinaryReader(File.OpenRead(archivePath));

            long metadataPosition = input.ReadInt64();
            input.BaseStream.Seek(metadataPosition, SeekOrigin.Begin);
            var metadata = ReadMetadata(input);

            if (!string.IsNullOrWhiteSpace(metadata.PasswordHash))
            {
                string enteredPassword = PasswordDialog.RequestPassword("Please enter archive password:");
                if (string.IsNullOrWhiteSpace(enteredPassword))
                {
                    throw new UnauthorizedAccessException("Password is required to extract files.");
                }

                string enteredHash = ComputeSha256Hash.Make(enteredPassword);
                if (enteredHash != metadata.PasswordHash)
                {
                    throw new UnauthorizedAccessException("Incorrect password.");
                }
            }

            string outputDir = outputDirectory ?? Path.GetDirectoryName(archivePath);
            var algorithm = Enum.Parse<CompressorType>(metadata.Algorithm);

            for (int i = 0; i < metadata.Entries.Count; i++)
            {
                var entry = metadata.Entries[i];

                input.BaseStream.Seek(entry.DataOffset, SeekOrigin.Begin);
                byte[] compressedData = input.ReadBytes((int)entry.CompressedDataLength);

                byte[] decompressedData = await DecompressFileData(compressedData, entry.Frequencies, algorithm, i,
                    metadata.Entries.Count);

                string outputPath = Path.Combine(outputDir,
                    $"{Path.GetFileNameWithoutExtension(entry.FileName)}_decompressed{Path.GetExtension(entry.FileName)}");
                File.WriteAllBytes(outputPath, decompressedData);

                extractedFiles.Add(outputPath);
            }

            return extractedFiles.ToArray();
        });
    }

    public List<ArchiveEntry> ListFiles(string archivePath)
    {
        using var input = new BinaryReader(File.OpenRead(archivePath));

        long metadataPosition = input.ReadInt64();
        input.BaseStream.Seek(metadataPosition, SeekOrigin.Begin);
        var metadata = ReadMetadata(input);

        return metadata.Entries;
    }

    private async Task<byte[]> DecompressFileData(byte[] compressedData, Dictionary<byte, int> frequencies,
        CompressorType algorithm, int i, int totalFiles)
    {
        if (algorithm == CompressorType.Huffman)
        {
            _huffmanCompressor.ProgressChanged += p =>
            {
                var progress = (p / 100) * (100 / totalFiles) + (100 / totalFiles) * i;
                ProgressChangedHuffman?.Invoke(progress);
            };
            var result = await _huffmanCompressor.DecompressWithHuffman(compressedData, frequencies);
            _huffmanCompressor.ProgressChanged -= ProgressChangedHuffman;
            return result;
        }

        if (algorithm == CompressorType.ShannonFano)
        {
            _shannonFanoCompressor.ProgressChanged += p =>
            {
                var progress = (p / 100) * (100 / totalFiles) + (100 / totalFiles) * i;
                ProgressChangedShannonFano?.Invoke(progress);
            };
            var result = await _shannonFanoCompressor.DecompressWithShannonFano(compressedData, frequencies);
            _shannonFanoCompressor.ProgressChanged -= ProgressChangedShannonFano;
            return result;
        }

        throw new NotSupportedException($"Algorithm {algorithm} is not supported for archives.");
    }


    private void WriteMetadata(BinaryWriter writer, ArchiveMetadata metadata)
    {
        writer.Write(metadata.Algorithm);
        writer.Write(metadata.PasswordHash);
        writer.Write(metadata.Entries.Count);

        foreach (var entry in metadata.Entries)
        {
            writer.Write(entry.FileName);
            writer.Write(entry.RelativePath);
            writer.Write(entry.OriginalSize);
            writer.Write(entry.CompressedSize);
            writer.Write(entry.DataOffset);
            writer.Write(entry.CompressedDataLength);

            writer.Write(entry.Frequencies.Count);
            foreach (var freq in entry.Frequencies)
            {
                writer.Write(freq.Key);
                writer.Write(freq.Value);
            }
        }
    }

    private ArchiveMetadata ReadMetadata(BinaryReader reader)
    {
        var metadata = new ArchiveMetadata
        {
            Algorithm = reader.ReadString(),
            PasswordHash = reader.ReadString(),
        };

        int entryCount = reader.ReadInt32();
        for (int i = 0; i < entryCount; i++)
        {
            var entry = new ArchiveEntry
            {
                FileName = reader.ReadString(),
                RelativePath = reader.ReadString(),
                OriginalSize = reader.ReadInt64(),
                CompressedSize = reader.ReadInt64(),
                DataOffset = reader.ReadInt64(),
                CompressedDataLength = reader.ReadInt64(),
            };

            int freqCount = reader.ReadInt32();
            for (int j = 0; j < freqCount; j++)
            {
                byte b = reader.ReadByte();
                int f = reader.ReadInt32();
                entry.Frequencies[b] = f;
            }

            metadata.Entries.Add(entry);
        }

        return metadata;
    }

    public void Pause()
    {
        _huffmanCompressor.Pause();
        _shannonFanoCompressor.Pause();
    }

    public void Resume()
    {
        _huffmanCompressor.Resume();
        _shannonFanoCompressor.Resume();
    }

    public void Cancel()
    {
        _huffmanCompressor.Cancel();
        _shannonFanoCompressor.Cancel();
    }
}