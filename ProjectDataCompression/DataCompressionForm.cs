using System.Media;
using ProjectDataCompression.Enums;

namespace ProjectDataCompression.Models;

public partial class DataCompressionForm : Form
{
    private ArchiveCompressor _archive = new();

    private Button btnSelectSingleFile;
    private Button btnSelectMultipleFiles;
    private Button btnSelectCompressedFile;
    private TextBox txtInputPath;
    private TextBox txtInputPassword;
    private ComboBox comboBoxAlgorithm;
    private Button btnCompress;
    private Button btnDecompress;
    private Button btnExtractSingle;
    private Button btnPause;
    private Button btnResume;
    private Button btnCancel;
    private ListBox listBoxResults;
    private ListBox listBoxArchiveFiles;
    private ProgressBar progressBarHuffman;
    private ProgressBar progressBarShannon;
    private Label lblHuffmanProgress;
    private Label lblShannonProgress;
    private Label lblArchiveProgress;

    private string[] _selectedFiles = new string[0];

    public DataCompressionForm()
    {
        InitializeComponent();
        InitComponent();

        comboBoxAlgorithm.DataSource = Enum.GetValues(typeof(CompressorType));

        SetProgressBar();
        SetInitialButtonStates();
    }

    private void SetInitialButtonStates()
    {
        // btnPause.Enabled = false;
        // btnResume.Enabled = false;
        // btnCancel.Enabled = false;
        // btnCompress.Enabled = false;
        // btnDecompress.Enabled = false;
        // btnExtractSingle.Enabled = false;
        // btnListFiles.Enabled = false;
    }

    private void SetProgressBar()
    {
        _archive.ProgressChangedHuffman += p =>
        {
            progressBarHuffman.Invoke(() =>
            {
                progressBarHuffman.Value = p;
                lblHuffmanProgress.Text = $"Huffman: {p}%";
            });
        };

        _archive.ProgressChangedShannonFano += p =>
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
        btnSelectSingleFile.Enabled = enabled;
        btnSelectMultipleFiles.Enabled = enabled;
        btnSelectCompressedFile.Enabled = enabled;
        comboBoxAlgorithm.Enabled = enabled;
        btnDecompress.Enabled = enabled;
        btnExtractSingle.Enabled = enabled;
        btnPause.Enabled = !enabled;
        btnResume.Enabled = !enabled;
        btnCancel.Enabled = !enabled;
    }

    private void BtnSelectSingleFileClick(object sender, EventArgs e)
    {
        OpenFileDialog ofd = new OpenFileDialog();
        ofd.Filter =
            "Common Files|*.txt;*.docx;*.pdf;*.png;*.jpg;*.jpeg;*.bmp;*.mp3;*.mp4;*.avi;*.csv;*.xlsx;*.json;*.xml|All Files (*.*)|*.*";

        if (ofd.ShowDialog() == DialogResult.OK)
        {
            txtInputPath.Text = ofd.FileName;
            _selectedFiles = new[] { ofd.FileName };
            btnCompress.Enabled = true;
        }
    }

    private void btnSelectMultipleFiles_Click(object sender, EventArgs e)
    {
        OpenFileDialog ofd = new OpenFileDialog();
        ofd.Filter = "All Files (*.*)|*.*";
        ofd.Multiselect = true;

        if (ofd.ShowDialog() == DialogResult.OK)
        {
            _selectedFiles = ofd.FileNames;
            txtInputPath.Text = $"{_selectedFiles.Length} files selected";

            listBoxArchiveFiles.Items.Clear();
            listBoxArchiveFiles.Items.Add("Selected Files:");
            foreach (var file in _selectedFiles)
            {
                listBoxArchiveFiles.Items.Add($"  • {Path.GetFileName(file)}");
            }

            btnCompress.Enabled = true;
            btnDecompress.Enabled = true;
        }
    }

    private void btnSelectCompressedFile_Click(object sender, EventArgs e)
    {
        OpenFileDialog ofd = new OpenFileDialog();
        ofd.Filter = $"Compressed Files (*.compress)|*.{ArchiveCompressor.CompressedFileExt}";

        if (ofd.ShowDialog() == DialogResult.OK)
        {
            txtInputPath.Text = ofd.FileName;
            _selectedFiles = new[] { ofd.FileName };
            btnDecompress.Enabled = true;
            btnCompress.Enabled = false;
            btnExtractSingle.Enabled = true;
            ShowListFiles();
            if (_selectedFiles.Length > 0)
            {
                btnExtractSingle.Visible = true;
            }
        }
    }

    private async void btnCompress_Click(object sender, EventArgs e)
    {
        if (_selectedFiles.Length == 0)
        {
            MessageBox.Show("Please select file(s) to compress.", "No Files Selected", MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        try
        {
            SetControlsEnabled(false);
            listBoxResults.Items.Clear();

            var algorithm = (CompressorType)comboBoxAlgorithm.SelectedItem!;
            var password = string.IsNullOrWhiteSpace(txtInputPassword.Text) ? null : txtInputPassword.Text;

            await CompressMultipleFiles(algorithm, password);

            SystemSounds.Asterisk.Play();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Compression failed: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            SetControlsEnabled(true);
        }
    }

    private async Task CompressMultipleFiles(CompressorType algorithm, string? password)
    {
        var outputFile = Path.Combine(Path.GetDirectoryName(_selectedFiles[0])!,
            $"Archive_{DateTime.Now:yyyyMMdd_HHmmss}.{ArchiveCompressor.CompressedFileExt}");

        var result = await _archive.CompressMultipleFilesAsync(_selectedFiles, outputFile, password, algorithm);

        listBoxResults.Items.Add($"Archive Compression Complete - {algorithm}");
        listBoxResults.Items.Add($"Files Compressed: {_selectedFiles.Length}");
        listBoxResults.Items.Add($"Original Size: {FormatBytes(result.OriginalSize)}");
        listBoxResults.Items.Add($"Compressed Size: {FormatBytes(result.CompressedSize)}");
        listBoxResults.Items.Add($"Compression Ratio: {result.CompressionRatio:F2}%");
        listBoxResults.Items.Add($"Duration: {result.Duration.TotalSeconds:F2} seconds");
        listBoxResults.Items.Add($"Output: {outputFile}");
    }

    private async void btnDecompress_Click(object sender, EventArgs e)
    {
        if (_selectedFiles.Length == 0)
        {
            MessageBox.Show("Please select a compressed file to decompress.", "No File Selected", MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        try
        {
            SetControlsEnabled(false);
            listBoxResults.Items.Clear();

            var inputFile = _selectedFiles[0];


            // ,Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            await new ArchiveCompressor().ExtractAllFilesAsync(inputFile);

            // if (result != null)
            // {
            //     listBoxResults.Items.Add($"Decompression Complete - {algorithm}");
            //     listBoxResults.Items.Add($"Compressed Size: {FormatBytes(result.CompressedSize)}");
            //     listBoxResults.Items.Add($"Decompressed Size: {FormatBytes(result.OriginalSize)}");
            //     listBoxResults.Items.Add($"Duration: {result.Duration.TotalSeconds:F2} seconds");
            //     listBoxResults.Items.Add($"Output: {outputFile}");
            // }
            SystemSounds.Asterisk.Play();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Decompression failed: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            SetControlsEnabled(true);
        }
    }

    private async void btnExtractSingle_Click(object sender, EventArgs e)
    {
        if (_selectedFiles.Length == 0)
        {
            MessageBox.Show("Please select an archive file.", "No Archive Selected", MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        var archivePath = _selectedFiles[0];

        try
        {
            var files = _archive.ListFiles(archivePath);

            if (files.Count == 0)
            {
                MessageBox.Show("No files found in the archive.", "Empty Archive", MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            var fileNames = files.Select(f => f.FileName).ToArray();
            var selectedFile = ShowFileSelectionDialog(fileNames);

            if (string.IsNullOrEmpty(selectedFile))
                return;

            SetControlsEnabled(false);
            listBoxResults.Items.Clear();

            var extractedPath = await _archive.ExtractSingleFileAsync(archivePath, selectedFile, null);

            listBoxResults.Items.Add($"File Extracted Successfully");
            listBoxResults.Items.Add($"File: {selectedFile}");
            listBoxResults.Items.Add($"Extracted To: {extractedPath}");

            SystemSounds.Asterisk.Play();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Extraction failed: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            SetControlsEnabled(true);
        }
    }

    private void ShowListFiles()
    {
        if (_selectedFiles.Length == 0)
        {
            MessageBox.Show("Please select an archive file.", "No Archive Selected", MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        try
        {
            var files = _archive.ListFiles(_selectedFiles[0]);

            listBoxArchiveFiles.Items.Clear();
            listBoxArchiveFiles.Items.Add("Archive Contents:");
            listBoxArchiveFiles.Items.Add("================");

            if (files.Count == 0)
            {
                listBoxArchiveFiles.Items.Add("No files found in the archive.");
            }
            else
            {
                foreach (var file in files)
                {
                    listBoxArchiveFiles.Items.Add($"📄 {file.FileName}");
                    listBoxArchiveFiles.Items.Add(
                        $"   Size: {FormatBytes(file.OriginalSize)} → {FormatBytes(file.CompressedSize)}");
                    listBoxArchiveFiles.Items.Add($"   Ratio: {(file.CompressedSize * 100.0 / file.OriginalSize):F1}%");
                    listBoxArchiveFiles.Items.Add($"   Modified: {file.LastModified:yyyy-MM-dd HH:mm:ss}");
                    listBoxArchiveFiles.Items.Add("");
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to list files: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }


    private string ShowFileSelectionDialog(string[] fileNames)
    {
        using var form = new Form
        {
            Text = "Select File to Extract",
            Size = new Size(400, 300),
            StartPosition = FormStartPosition.CenterParent
        };

        var listBox = new ListBox
        {
            Dock = DockStyle.Fill,
            SelectionMode = SelectionMode.One
        };

        listBox.Items.AddRange(fileNames);

        var panel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 40
        };

        var btnOK = new Button
        {
            Text = "OK",
            DialogResult = DialogResult.OK,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
            Location = new Point(panel.Width - 160, 10),
            Size = new Size(70, 25)
        };

        var btnCancel = new Button
        {
            Text = "Cancel",
            DialogResult = DialogResult.Cancel,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
            Location = new Point(panel.Width - 85, 10),
            Size = new Size(70, 25)
        };

        panel.Controls.AddRange(new Control[] { btnOK, btnCancel });
        form.Controls.AddRange(new Control[] { listBox, panel });

        form.AcceptButton = btnOK;
        form.CancelButton = btnCancel;

        if (form.ShowDialog() == DialogResult.OK && listBox.SelectedItem != null)
        {
            return listBox.SelectedItem.ToString();
        }

        return null;
    }

    private void btnPause_Click(object sender, EventArgs e)
    {
        _archive?.Pause();
        btnPause.Enabled = false;
        btnResume.Enabled = true;
    }

    private void btnResume_Click(object sender, EventArgs e)
    {
        _archive?.Resume();
        btnPause.Enabled = true;
        btnResume.Enabled = false;
    }

    private void btnCancel_Click(object sender, EventArgs e)
    {
        _archive?.Cancel();
        SetControlsEnabled(true);
    }

    private string FormatBytes(long bytes)
    {
        string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
        int counter = 0;
        double number = bytes;

        while (Math.Round(number / 1024) >= 1)
        {
            number /= 1024;
            counter++;
        }

        return $"{number:F2} {suffixes[counter]}";
    }

    private void InitComponent()
    {
        // This method would contain the Windows Forms Designer generated code
        // for initializing all the form controls
        SuspendLayout();

        // Form properties
        Text = "Data Compression Tool";
        Size = new Size(800, 600);
        StartPosition = FormStartPosition.CenterScreen;

        // Initialize controls (basic initialization - would be expanded in designer)

        // Algorithm Selection
        var lblAlgorithm = new Label();
        lblAlgorithm.Text = "Algorithm:";
        lblAlgorithm.Location = new Point(20, 20);
        lblAlgorithm.Size = new Size(70, 23);

        comboBoxAlgorithm = new ComboBox();
        comboBoxAlgorithm.Location = new Point(110, 20);
        comboBoxAlgorithm.Size = new Size(120, 23);
        comboBoxAlgorithm.DropDownStyle = ComboBoxStyle.DropDownList;

        // Password Input
        var lblPassword = new Label();
        lblPassword.Text = "Password:";
        lblPassword.Location = new Point(250, 20);
        lblPassword.Size = new Size(70, 23);
        lblPassword.TextAlign = ContentAlignment.MiddleLeft;

        txtInputPassword = new TextBox();
        txtInputPassword.Location = new Point(340, 20);
        txtInputPassword.Size = new Size(120, 23);
        // txtInputPassword.PasswordChar = '*';
        txtInputPassword.PlaceholderText = "Password";

        // File Selection Buttons
        btnSelectSingleFile = new Button();
        btnSelectSingleFile.Text = "Select Single File";
        btnSelectSingleFile.Location = new Point(20, 60);
        btnSelectSingleFile.Size = new Size(100, 30);
        btnSelectSingleFile.Click += BtnSelectSingleFileClick;

        btnSelectMultipleFiles = new Button();
        btnSelectMultipleFiles.Text = "Select Multiple Files";
        btnSelectMultipleFiles.Location = new Point(140, 60);
        btnSelectMultipleFiles.Size = new Size(150, 30);
        btnSelectMultipleFiles.Click += btnSelectMultipleFiles_Click;

        btnSelectCompressedFile = new Button();
        btnSelectCompressedFile.Text = "Select Compressed";
        btnSelectCompressedFile.Location = new Point(310, 60);
        btnSelectCompressedFile.Size = new Size(150, 30);
        btnSelectCompressedFile.Click += btnSelectCompressedFile_Click;

        // Input Path Display
        var lblInputPath = new Label();
        lblInputPath.Text = "Selected Path:";
        lblInputPath.Location = new Point(20, 100);
        lblInputPath.Size = new Size(100, 23);
        lblInputPath.TextAlign = ContentAlignment.MiddleLeft;

        txtInputPath = new TextBox();
        txtInputPath.Location = new Point(130, 100);
        txtInputPath.Size = new Size(570, 23);
        txtInputPath.ReadOnly = true;
        txtInputPath.BackColor = SystemColors.Control;

        // Action Buttons
        btnCompress = new Button();
        btnCompress.Text = "Compress";
        btnCompress.Location = new Point(20, 140);
        btnCompress.Size = new Size(80, 35);
        btnCompress.UseVisualStyleBackColor = true;
        btnCompress.Click += btnCompress_Click;

        btnDecompress = new Button();
        btnDecompress.Text = "Decompress";
        btnDecompress.Location = new Point(110, 140);
        btnDecompress.Size = new Size(90, 35);
        btnDecompress.UseVisualStyleBackColor = true;
        btnDecompress.Click += btnDecompress_Click;

        btnExtractSingle = new Button();
        btnExtractSingle.Text = "Extract Single";
        btnExtractSingle.Location = new Point(210, 140);
        btnExtractSingle.Size = new Size(100, 35);
        btnExtractSingle.UseVisualStyleBackColor = true;
        btnExtractSingle.Click += btnExtractSingle_Click;
        btnExtractSingle.Visible = false;

        // Control Buttons
        btnPause = new Button();
        btnPause.Text = "Pause";
        btnPause.Location = new Point(450, 140);
        btnPause.Size = new Size(70, 35);
        btnPause.UseVisualStyleBackColor = true;
        btnPause.Click += btnPause_Click;

        btnResume = new Button();
        btnResume.Text = "Resume";
        btnResume.Location = new Point(530, 140);
        btnResume.Size = new Size(70, 35);
        btnResume.UseVisualStyleBackColor = true;
        btnResume.Click += btnResume_Click;

        btnCancel = new Button();
        btnCancel.Text = "Cancel";
        btnCancel.Location = new Point(610, 140);
        btnCancel.Size = new Size(70, 35);
        btnCancel.UseVisualStyleBackColor = true;
        btnCancel.Click += btnCancel_Click;

        // Progress Bars and Labels
        lblHuffmanProgress = new Label();
        lblHuffmanProgress.Text = "Huffman: 0%";
        lblHuffmanProgress.Location = new Point(20, 190);
        lblHuffmanProgress.Size = new Size(150, 20);
        lblHuffmanProgress.TextAlign = ContentAlignment.MiddleLeft;

        progressBarHuffman = new ProgressBar();
        progressBarHuffman.Location = new Point(180, 190);
        progressBarHuffman.Size = new Size(200, 20);
        progressBarHuffman.Maximum = 100;
        progressBarHuffman.Style = ProgressBarStyle.Continuous;

        lblShannonProgress = new Label();
        lblShannonProgress.Text = "Shannon-Fano: 0%";
        lblShannonProgress.Location = new Point(20, 220);
        lblShannonProgress.Size = new Size(150, 20);
        lblShannonProgress.TextAlign = ContentAlignment.MiddleLeft;

        progressBarShannon = new ProgressBar();
        progressBarShannon.Location = new Point(180, 220);
        progressBarShannon.Size = new Size(200, 20);
        progressBarShannon.Maximum = 100;
        progressBarShannon.Style = ProgressBarStyle.Continuous;

        lblArchiveProgress = new Label();
        lblArchiveProgress.Text = "Archive: 0%";
        lblArchiveProgress.Location = new Point(20, 250);
        lblArchiveProgress.Size = new Size(150, 20);
        lblArchiveProgress.TextAlign = ContentAlignment.MiddleLeft;
        lblArchiveProgress.Visible = false;

        // Results Section
        var lblResults = new Label();
        lblResults.Text = "Results:";
        lblResults.Location = new Point(20, 280);
        lblResults.Size = new Size(100, 23);
        lblResults.TextAlign = ContentAlignment.MiddleLeft;
        lblResults.Font = new Font(lblResults.Font, FontStyle.Bold);

        listBoxResults = new ListBox();
        listBoxResults.Location = new Point(20, 310);
        listBoxResults.Size = new Size(420, 320);
        listBoxResults.HorizontalScrollbar = true;
        listBoxResults.Font = new Font("Consolas", 9);

        // Archive Files Section

        listBoxArchiveFiles = new ListBox();
        listBoxArchiveFiles.Location = new Point(460, 310);
        listBoxArchiveFiles.Size = new Size(400, 320);
        listBoxArchiveFiles.HorizontalScrollbar = true;
        listBoxArchiveFiles.Font = new Font("Consolas", 9);

        // Basic layout (would be properly positioned in designer)
        Controls.AddRange(btnSelectSingleFile, btnSelectMultipleFiles, btnSelectCompressedFile,
            txtInputPath, txtInputPassword, lblAlgorithm, comboBoxAlgorithm, btnCompress, btnDecompress,
            btnExtractSingle, btnPause, btnResume, btnCancel, listBoxResults, listBoxArchiveFiles,
            progressBarHuffman, progressBarShannon, lblHuffmanProgress, lblShannonProgress,
            lblArchiveProgress);

        ResumeLayout(false);
    }
}