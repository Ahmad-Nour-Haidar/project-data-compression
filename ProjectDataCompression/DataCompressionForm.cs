// File: DataCompressionForm.cs 
using ProjectDataCompression.Algorithms;
using ProjectDataCompression.Enums;
using ProjectDataCompression.Models;

namespace ProjectDataCompression;

public partial class DataCompressionForm : Form
{
    private HuffmanCompressor _huffman = new();
    private ShannonFanoCompressor _shannon = new();

    private Button btnSelectFile;
    private TextBox txtInputPath;
    private ComboBox comboBoxAlgorithm;
    private Button btnCompress;
    private Button btnDecompress;
    private Button btnPause;
    private Button btnResume;
    private Button btnCancel;
    private ListBox listBoxResults;
    private ProgressBar progressBarHuffman;
    private ProgressBar progressBarShannon;
    private Label lblHuffmanProgress;
    private Label lblShannonProgress;

    private CancellationTokenSource _cts;
    private ManualResetEventSlim _pauseEvent = new(true);

    public DataCompressionForm()
    {
        InitializeComponent();
        InitComponent();
        comboBoxAlgorithm.DataSource = Enum.GetValues(typeof(CompressorType));

        _huffman.ProgressChanged += p =>
        {
            progressBarHuffman.Invoke(() =>
            {
                progressBarHuffman.Value = p;
                lblHuffmanProgress.Text = $"Huffman: {p}%";
            });
        };

        _shannon.ProgressChanged += p =>
        {
            progressBarShannon.Invoke(() =>
            {
                progressBarShannon.Value = p;
                lblShannonProgress.Text = $"Shannon-Fano: {p}%";
            });
        };
    }

    private void SetControlsEnabled(bool enabled)
    {
        btnCompress.Enabled = enabled;
        btnSelectFile.Enabled = enabled;
        comboBoxAlgorithm.Enabled = enabled;
        btnDecompress.Enabled = enabled;
        btnPause.Enabled = !enabled;
        btnResume.Enabled = !enabled;
        btnCancel.Enabled = !enabled;
    }
    
    private void btnSelectFile_Click(object sender, EventArgs e)
    {
        using OpenFileDialog ofd = new OpenFileDialog();
        ofd.Filter = "Text files (*.txt)|*.txt|Compressed files (*.compress)|*.compress|All files (*.*)|*.*";
        ofd.Title = "Select a file to compress or decompress";

        if (ofd.ShowDialog() == DialogResult.OK)
        {
            txtInputPath.Text = ofd.FileName;
        }
    }

    private async void btnCompress_Click(object sender, EventArgs e)
    {
        string inputPath = txtInputPath.Text;
        if (!File.Exists(inputPath))
        {
            MessageBox.Show("Invalid file path.");
            return;
        }

        _cts = new();
        _pauseEvent.Set();

        SetControlsEnabled(false);
        string fileName = Path.GetFileNameWithoutExtension(inputPath);
        string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        CompressorType selected = (CompressorType)comboBoxAlgorithm.SelectedItem;

        listBoxResults.Items.Clear();
        progressBarHuffman.Value = 0;
        progressBarShannon.Value = 0;
        lblHuffmanProgress.Text = "Huffman: 0%";
        lblShannonProgress.Text = "Shannon-Fano: 0%";

        try
        {
            if (selected == CompressorType.Huffman || selected == CompressorType.Both)
            {
                string outHuffman = Path.Combine(desktop, fileName + "_compressed_huffman.compress");
                CompressionResult res = await _huffman.CompressAsync(inputPath, outHuffman);
                DisplayResult(res, "Huffman");
            }

            if (selected == CompressorType.ShannonFano || selected == CompressorType.Both)
            {
                string outShannon = Path.Combine(desktop, fileName + "_compressed_shannon.compress");
                CompressionResult res = await _shannon.CompressAsync(inputPath, outShannon);
                DisplayResult(res, "Shannon-Fano");
            }

            System.Media.SystemSounds.Exclamation.Play();
            MessageBox.Show("Compression finished!", "Done");
        }
        catch (OperationCanceledException)
        {
            MessageBox.Show("Operation canceled.", "Canceled");
        }

        progressBarHuffman.Value = 0;
        progressBarShannon.Value = 0;
        lblHuffmanProgress.Text = "Huffman: 0%";
        lblShannonProgress.Text = "Shannon-Fano: 0%";
        SetControlsEnabled(true);
    }

    private async void btnDecompress_Click(object sender, EventArgs e)
    {
        string inputPath = txtInputPath.Text;
        if (!File.Exists(inputPath) || Path.GetExtension(inputPath) != ".compress")
        {
            MessageBox.Show("Please select a valid .compress file.");
            return;
        }

        SetControlsEnabled(false);
        string fileName = Path.GetFileNameWithoutExtension(inputPath).Replace("_compressed_huffman", "").Replace("_compressed_shannon", "");
        string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        string outputPath = Path.Combine(desktop, fileName + "_decompressed.txt");

        try
        {
            if (inputPath.Contains("huffman"))
            {
                await _huffman.DecompressAsync(inputPath, outputPath);
                MessageBox.Show("Huffman decompression completed.", "Done");
            }
            else if (inputPath.Contains("shannon"))
            {
                await _shannon.DecompressAsync(inputPath, outputPath);
                MessageBox.Show("Shannon-Fano decompression completed.", "Done");
            }
            else
            {
                MessageBox.Show("Unknown compression type.");
            }
            System.Media.SystemSounds.Asterisk.Play();
        }
        finally
        {
            progressBarHuffman.Value = 0;
            progressBarShannon.Value = 0;
            lblHuffmanProgress.Text = "Huffman: 0%";
            lblShannonProgress.Text = "Shannon-Fano: 0%";
            SetControlsEnabled(true);
        }
    }

    private void btnPause_Click(object sender, EventArgs e) => _pauseEvent.Reset();
    private void btnResume_Click(object sender, EventArgs e) => _pauseEvent.Set();
    private void btnCancel_Click(object sender, EventArgs e) => _cts?.Cancel();

    private void DisplayResult(CompressionResult result, string label)
    {
        listBoxResults.Items.Add($"{label}:");
        listBoxResults.Items.Add($"Original Size: {result.OriginalSize} bytes");
        listBoxResults.Items.Add($"Compressed Size: {result.CompressedSize} bytes");
        listBoxResults.Items.Add($"Compression Ratio: {result.CompressionRatio:P2}");
        listBoxResults.Items.Add($"Time Taken: {result.Duration.TotalSeconds:F2} seconds");
        listBoxResults.Items.Add("-----------------------------");
    }

    private void InitComponent()
    {
        btnSelectFile = new(); txtInputPath = new(); comboBoxAlgorithm = new();
        btnCompress = new(); btnDecompress = new(); listBoxResults = new();
        progressBarHuffman = new(); progressBarShannon = new();
        btnPause = new(); btnResume = new(); btnCancel = new();
        lblHuffmanProgress = new(); lblShannonProgress = new();

        SuspendLayout();

        btnSelectFile.SetBounds(20, 20, 100, 30);
        btnSelectFile.Text = "Select File";
        btnSelectFile.Click += btnSelectFile_Click;

        txtInputPath.SetBounds(130, 25, 400, 20); txtInputPath.ReadOnly = true;

        comboBoxAlgorithm.SetBounds(20, 70, 150, 20);

        btnCompress.SetBounds(200, 70, 100, 30); btnCompress.Text = "Compress"; btnCompress.Click += btnCompress_Click;
        btnDecompress.SetBounds(320, 70, 100, 30); btnDecompress.Text = "Decompress"; btnDecompress.Click += btnDecompress_Click;

        btnPause.SetBounds(430, 70, 60, 30); btnPause.Text = "Pause"; btnPause.Click += btnPause_Click;
        btnResume.SetBounds(500, 70, 60, 30); btnResume.Text = "Resume"; btnResume.Click += btnResume_Click;
        btnCancel.SetBounds(570, 70, 60, 30); btnCancel.Text = "Cancel"; btnCancel.Click += btnCancel_Click;

        lblHuffmanProgress.SetBounds(20, 100, 250, 15); lblHuffmanProgress.Text = "Huffman: 0%";
        progressBarHuffman.SetBounds(20, 120, 610, 15);

        lblShannonProgress.SetBounds(20, 145, 250, 15); lblShannonProgress.Text = "Shannon-Fano: 0%";
        progressBarShannon.SetBounds(20, 165, 610, 15);

        listBoxResults.SetBounds(20, 190, 610, 170);

        ClientSize = new Size(660, 380);
        Controls.AddRange([btnSelectFile, txtInputPath, comboBoxAlgorithm,
            btnCompress, btnDecompress, btnPause, btnResume, btnCancel,
            lblHuffmanProgress, progressBarHuffman, lblShannonProgress, progressBarShannon, listBoxResults]);
        Text = "File Compressor - Huffman & Shannon-Fano";
        ResumeLayout(false); PerformLayout();
    }
}