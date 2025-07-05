// File: DataCompressionForm.cs 

using System.Media;
using ProjectDataCompression.Algorithms;
using ProjectDataCompression.Enums;
using ProjectDataCompression.Models;

namespace ProjectDataCompression;

public partial class DataCompressionForm : Form
{
    private HuffmanCompressor? _huffman;
    private ShannonFanoCompressor? _shannon;

    private Button btnSelectFileAny;
    private Button btnSelectCompressedFile;
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

    // private CancellationTokenSource _cts;
    // private ManualResetEventSlim _pauseEvent = new(true);

    public DataCompressionForm()
    {
        InitializeComponent();
        InitComponent();
        comboBoxAlgorithm.DataSource = Enum.GetValues(typeof(CompressorType));
        SetProgressBar();
        btnPause.Enabled = false;
        btnResume.Enabled = false;
        btnCancel.Enabled = false;
        btnCompress.Enabled = false;
        btnDecompress.Enabled = false;
    }

    private void SetProgressBar()
    {
        if (_huffman != null)
        {
            _huffman.ProgressChanged += p =>
            {
                progressBarHuffman.Invoke(() =>
                {
                    progressBarHuffman.Value = p;
                    lblHuffmanProgress.Text = $"Huffman: {p}%";
                });
            };
        }

        if (_shannon != null)
        {
            _shannon.ProgressChanged += p =>
            {
                progressBarShannon.Invoke(() =>
                {
                    progressBarShannon.Value = p;
                    lblShannonProgress.Text = $"Shannon-Fano: {p}%";
                });
            };
        }
    }

    private void SetControlsEnabled(bool enabled)
    {
        btnCompress.Enabled = enabled;
        btnSelectFileAny.Enabled = enabled;
        btnSelectCompressedFile.Enabled = enabled;
        comboBoxAlgorithm.Enabled = enabled;
        btnDecompress.Enabled = enabled;
        btnPause.Enabled = !enabled;
        btnResume.Enabled = !enabled;
        btnCancel.Enabled = !enabled;
    }

    private void btnSelectFileAny_Click(object sender, EventArgs e)
    {
        OpenFileDialog ofd = new OpenFileDialog();
        ofd.Filter =
            "Common Files|*.txt;*.docx;*.pdf;*.png;*.jpg;*.jpeg;*.bmp;*.mp3;*.mp4;*.avi;*.csv;*.xlsx;*.json;*.xml|All Files (*.*)|*.*";

        if (ofd.ShowDialog() == DialogResult.OK)
        {
            txtInputPath.Text = ofd.FileName;
            btnCompress.Enabled = true;
        }
        else
        {
            // Play sound if a user cancels
            SystemSounds.Hand.Play(); 
        }
    }

    private void btnSelectCompressedFile_Click(object sender, EventArgs e)
    {
        OpenFileDialog ofd = new OpenFileDialog();
        ofd.Filter = "Compressed Files (*.compress)|*.compress";
        if (ofd.ShowDialog() == DialogResult.OK)
        {
            txtInputPath.Text = ofd.FileName;
            btnDecompress.Enabled = true;
        }
        else
        {
            SystemSounds.Exclamation.Play();
        }
    }


    private async void btnCompress_Click(object sender, EventArgs e)
    {
        string inputPath = txtInputPath.Text;
        if (!File.Exists(inputPath))
        {
            SystemSounds.Hand.Play();
            MessageBox.Show("Invalid file path.");
            return;
        }

        SetControlsEnabled(false);
        btnPause.Enabled = true;
        btnResume.Enabled = false;
        btnCancel.Enabled = true;
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
                _huffman = new();
                SetProgressBar();
                CompressionResult res = await _huffman.CompressAsync(inputPath, outHuffman);
                DisplayResult(res, "Huffman");
            }

            if (selected == CompressorType.ShannonFano || selected == CompressorType.Both)
            {
                string outShannon = Path.Combine(desktop, fileName + "_compressed_shannon.compress");
                _shannon = new();
                SetProgressBar();
                CompressionResult res = await _shannon.CompressAsync(inputPath, outShannon);
                DisplayResult(res, "Shannon-Fano");
            }

            _huffman = null;
            _shannon = null;
            SystemSounds.Exclamation.Play();
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
            SystemSounds.Hand.Play();
            MessageBox.Show("Please select a valid .compress file.");
            return;
        }

        SetControlsEnabled(false);
        string fileName = Path.GetFileNameWithoutExtension(inputPath).Replace("_compressed_huffman", "")
            .Replace("_compressed_shannon", "");
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

    private void btnPause_Click(object sender, EventArgs e)
    {
        btnPause.Enabled = false;
        btnResume.Enabled = true;
        _huffman?.Pause();
        _shannon?.Pause();
    }

    private void btnResume_Click(object sender, EventArgs e)
    {
        btnPause.Enabled = true;
        btnResume.Enabled = false;
        _huffman?.Resume();
        _shannon?.Resume();
    }

    private void btnCancel_Click(object sender, EventArgs e)
    {
        btnPause.Enabled = false;
        btnResume.Enabled = false;
        btnCancel.Enabled = false;
        _huffman?.Cancel();
        _shannon?.Cancel();
        _huffman = null;
        _shannon = null;
        progressBarHuffman.Value = 0;
        lblHuffmanProgress.Text = "Huffman: 0%";
        progressBarShannon.Value = 0;
        lblShannonProgress.Text = "Shannon-Fano: 0%";
        System.Media.SystemSounds.Hand.Play();
    }

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
        btnSelectFileAny = new();
        btnSelectCompressedFile = new();
        txtInputPath = new();
        comboBoxAlgorithm = new();
        btnCompress = new();
        btnDecompress = new();
        listBoxResults = new();
        progressBarHuffman = new();
        progressBarShannon = new();
        btnPause = new();
        btnResume = new();
        btnCancel = new();
        lblHuffmanProgress = new();
        lblShannonProgress = new();

        SuspendLayout();

        // btnSelectFileAny
        btnSelectFileAny = new Button();
        btnSelectFileAny.Location = new Point(20, 20);
        btnSelectFileAny.Size = new Size(120, 30);
        btnSelectFileAny.Text = "Select File";
        btnSelectFileAny.Click += btnSelectFileAny_Click;
        Controls.Add(btnSelectFileAny);

        // btnSelectCompressedFile
        btnSelectCompressedFile = new Button();
        btnSelectCompressedFile.Location = new Point(150, 20);
        btnSelectCompressedFile.Size = new Size(160, 30);
        btnSelectCompressedFile.Text = "Select .compress File";
        btnSelectCompressedFile.Click += btnSelectCompressedFile_Click;
        Controls.Add(btnSelectCompressedFile);

        txtInputPath.Location = new(320, 20);
        txtInputPath.ReadOnly = true;
        txtInputPath.AutoSize = true;
        txtInputPath.Size = new(400, 20);

        comboBoxAlgorithm.SetBounds(20, 70, 150, 20);

        btnCompress.SetBounds(200, 70, 100, 30);
        btnCompress.Text = "Compress";
        btnCompress.Click += btnCompress_Click;
        btnDecompress.SetBounds(320, 70, 100, 30);
        btnDecompress.Text = "Decompress";
        btnDecompress.Click += btnDecompress_Click;

        btnPause.SetBounds(430, 70, 60, 30);
        btnPause.Text = "Pause";
        btnPause.Click += btnPause_Click;
        btnResume.SetBounds(500, 70, 60, 30);
        btnResume.Text = "Resume";
        btnResume.Click += btnResume_Click;
        btnCancel.SetBounds(570, 70, 60, 30);
        btnCancel.Text = "Cancel";
        btnCancel.Click += btnCancel_Click;

        lblHuffmanProgress.SetBounds(20, 100, 250, 20);
        lblHuffmanProgress.Text = "Huffman: 0%";
        progressBarHuffman.SetBounds(20, 120, 610, 20);

        lblShannonProgress.SetBounds(20, 155, 250, 20);
        lblShannonProgress.Text = "Shannon-Fano: 0%";
        progressBarShannon.SetBounds(20, 175, 610, 20);

        listBoxResults.SetBounds(20, 220, 610, 400);

        ClientSize = new Size(880, 660);
        Controls.AddRange(btnSelectFileAny, btnSelectCompressedFile, txtInputPath, comboBoxAlgorithm, btnCompress,
            btnDecompress, btnPause,
            btnResume, btnCancel, lblHuffmanProgress, progressBarHuffman, lblShannonProgress, progressBarShannon,
            listBoxResults);
        Text = "File Compressor - Huffman & Shannon-Fano";
        ResumeLayout(false);
        PerformLayout();
    }
}