using System.Media;
using ProjectDataCompression.Algorithms;
using ProjectDataCompression.Enums;

namespace ProjectDataCompression;

public partial class DataCompressionForm : Form
{
    private readonly ArchiveCompressor _archive = new();

    private Button _btnSelectSingleFile;
    private Button _btnSelectMultipleFiles;
    private Button _btnSelectCompressedFile;
    private TextBox _txtInputPath;
    private TextBox _txtInputPassword;
    private ComboBox _comboBoxAlgorithm;
    private Button _btnCompress;
    private Button _btnDecompress;
    private Button _btnExtractSingle;
    private Button _btnPause;
    private Button _btnResume;
    private Button _btnCancel;
    private ListBox _listBoxResults;
    private ListBox _listBoxArchiveFiles;
    private ProgressBar _progressBarHuffman;
    private ProgressBar _progressBarShannon;
    private Label _lblHuffmanProgress;
    private Label _lblShannonProgress;

    private string[] _selectedFiles = [];

    public DataCompressionForm()
    {
        InitializeComponent();
        InitComponent();

        _comboBoxAlgorithm!.DataSource = Enum.GetValues(typeof(CompressorType));

        SetProgressBar();
    }

    private void SetProgressBar()
    {
        _archive.ProgressChangedHuffman += p =>
        {
            _progressBarHuffman.Invoke(() =>
            {
                _progressBarHuffman.Value = p;
                _lblHuffmanProgress.Text = $"Huffman: {p}%";
            });
        };

        _archive.ProgressChangedShannonFano += p =>
        {
            _progressBarShannon.Invoke(() =>
            {
                _progressBarShannon.Value = p;
                _lblShannonProgress.Text = $"Shannon-Fano: {p}%";
            });
        };
    }

    private void SetControlsEnabled(bool enabled)
    {
        _btnCompress.Enabled = enabled;
        _btnSelectSingleFile.Enabled = enabled;
        _btnSelectMultipleFiles.Enabled = enabled;
        _btnSelectCompressedFile.Enabled = enabled;
        _comboBoxAlgorithm.Enabled = enabled;
        _btnDecompress.Enabled = enabled;
        _btnExtractSingle.Enabled = enabled;
        _btnPause.Enabled = !enabled;
        _btnResume.Enabled = !enabled;
        _btnCancel.Enabled = !enabled;
    }

    private void BtnSelectSingleFileClick(object sender, EventArgs e)
    {
        OpenFileDialog ofd = new OpenFileDialog();
        ofd.Filter =
            "Common Files|*.txt;*.docx;*.pdf;*.png;*.jpg;*.jpeg;*.bmp;*.mp3;*.mp4;*.avi;*.csv;*.xlsx;*.json;*.xml|All Files (*.*)|*.*";

        if (ofd.ShowDialog() == DialogResult.OK)
        {
            _txtInputPath.Text = ofd.FileName;
            _selectedFiles = [ofd.FileName];
            _btnCompress.Enabled = true;
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
            _txtInputPath.Text = $"{_selectedFiles.Length} files selected";

            _listBoxArchiveFiles.Items.Clear();
            _listBoxArchiveFiles.Items.Add("Selected Files:");
            foreach (var file in _selectedFiles)
            {
                _listBoxArchiveFiles.Items.Add($"  • {Path.GetFileName(file)}");
            }

            _btnCompress.Enabled = true;
            _btnDecompress.Enabled = true;
        }
    }

    private void btnSelectCompressedFile_Click(object sender, EventArgs e)
    {
        OpenFileDialog ofd = new OpenFileDialog();
        ofd.Filter = $"Compressed Files (*.compress)|*.{ArchiveCompressor.CompressedFileExt}";

        if (ofd.ShowDialog() == DialogResult.OK)
        {
            _txtInputPath.Text = ofd.FileName;
            _selectedFiles = [ofd.FileName];
            _btnDecompress.Enabled = true;
            _btnCompress.Enabled = false;
            _btnExtractSingle.Enabled = true;
            ShowListFiles();
            if (_selectedFiles.Length > 0)
            {
                _btnExtractSingle.Visible = true;
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
            _listBoxResults.Items.Clear();

            var algorithm = (CompressorType)_comboBoxAlgorithm.SelectedItem!;
            var password = string.IsNullOrWhiteSpace(_txtInputPassword.Text) ? null : _txtInputPassword.Text;

            if (algorithm == CompressorType.Both)
            {
                var huffmanTask = CompressMultipleFiles(CompressorType.Huffman, password);
                var shannonTask = CompressMultipleFiles(CompressorType.ShannonFano, password);
                await Task.WhenAll(huffmanTask, shannonTask);
            }
            else
            {
                await CompressMultipleFiles(algorithm, password);
            }

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
            $"Archive_{DateTime.Now:yyyyMMdd_HHmmss}{Path.GetFileNameWithoutExtension(Path.GetRandomFileName())}.{ArchiveCompressor.CompressedFileExt}");

        var result = await _archive.CompressMultipleFilesAsync(_selectedFiles, outputFile, password, algorithm);

        _listBoxResults.Items.Add("\n=================================");
        _listBoxResults.Items.Add($"Archive Compression Complete - {algorithm}");
        _listBoxResults.Items.Add($"Files Compressed: {_selectedFiles.Length}");
        _listBoxResults.Items.Add($"Original Size: {FormatBytes(result.OriginalSize)}");
        _listBoxResults.Items.Add($"Compressed Size: {FormatBytes(result.CompressedSize)}");
        _listBoxResults.Items.Add($"Compression Ratio: {result.CompressionRatio:F2}%");
        _listBoxResults.Items.Add($"Duration: {result.Duration.TotalSeconds:F2} seconds");
        _listBoxResults.Items.Add($"Output: {outputFile}");
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
            _listBoxResults.Items.Clear();

            var inputFile = _selectedFiles[0];
            
            await new ArchiveCompressor().ExtractAllFilesAsync(inputFile);
            
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
            _listBoxResults.Items.Clear();

            var extractedPath = await _archive.ExtractSingleFileAsync(archivePath, selectedFile, null);

            _listBoxResults.Items.Add($"File Extracted Successfully");
            _listBoxResults.Items.Add($"File: {selectedFile}");
            _listBoxResults.Items.Add($"Extracted To: {extractedPath}");

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

            _listBoxArchiveFiles.Items.Clear();
            _listBoxArchiveFiles.Items.Add("Archive Contents:");
            _listBoxArchiveFiles.Items.Add("================");

            if (files.Count == 0)
            {
                _listBoxArchiveFiles.Items.Add("No files found in the archive.");
            }
            else
            {
                foreach (var file in files)
                {
                    _listBoxArchiveFiles.Items.Add($"📄 {file.FileName}");
                    _listBoxArchiveFiles.Items.Add(
                        $"   Size: {FormatBytes(file.OriginalSize)} → {FormatBytes(file.CompressedSize)}");
                    _listBoxArchiveFiles.Items.Add($"   Ratio: {(file.CompressedSize * 100.0 / file.OriginalSize):F1}%");
                    _listBoxArchiveFiles.Items.Add("");
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
        using var form = new Form();
        form.Text = "Select File to Extract";
        form.Size = new Size(400, 300);
        form.StartPosition = FormStartPosition.CenterParent;

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

        var btnOk = new Button
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

        panel.Controls.AddRange(btnOk, btnCancel);
        form.Controls.AddRange(listBox, panel);

        form.AcceptButton = btnOk;
        form.CancelButton = btnCancel;

        if (form.ShowDialog() == DialogResult.OK && listBox.SelectedItem != null)
        {
            return listBox.SelectedItem.ToString()!;
        }

        return null;
    }

    private void btnPause_Click(object sender, EventArgs e)
    {
        _archive.Pause();
        _btnPause.Enabled = false;
        _btnResume.Enabled = true;
    }

    private void btnResume_Click(object sender, EventArgs e)
    {
        _archive.Resume();
        _btnPause.Enabled = true;
        _btnResume.Enabled = false;
    }

    private void btnCancel_Click(object sender, EventArgs e)
    {
        _archive.Cancel();
        SetControlsEnabled(true);
    }

    private string FormatBytes(long bytes)
    {
        string[] suffixes = ["B", "KB", "MB", "GB", "TB"];
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

        _comboBoxAlgorithm = new ComboBox();
        _comboBoxAlgorithm.Location = new Point(110, 20);
        _comboBoxAlgorithm.Size = new Size(120, 23);
        _comboBoxAlgorithm.DropDownStyle = ComboBoxStyle.DropDownList;

        // Password Input
        var lblPassword = new Label();
        lblPassword.Text = "Password:";
        lblPassword.Location = new Point(250, 20);
        lblPassword.Size = new Size(70, 23);
        lblPassword.TextAlign = ContentAlignment.MiddleLeft;

        _txtInputPassword = new TextBox();
        _txtInputPassword.Location = new Point(340, 20);
        _txtInputPassword.Size = new Size(120, 23);
        // txtInputPassword.PasswordChar = '*';
        _txtInputPassword.PlaceholderText = "Password";

        // File Selection Buttons
        _btnSelectSingleFile = new Button();
        _btnSelectSingleFile.Text = "Select Single File";
        _btnSelectSingleFile.Location = new Point(20, 60);
        _btnSelectSingleFile.Size = new Size(100, 30);
        _btnSelectSingleFile.Click += BtnSelectSingleFileClick!;

        _btnSelectMultipleFiles = new Button();
        _btnSelectMultipleFiles.Text = "Select Multiple Files";
        _btnSelectMultipleFiles.Location = new Point(140, 60);
        _btnSelectMultipleFiles.Size = new Size(150, 30);
        _btnSelectMultipleFiles.Click += btnSelectMultipleFiles_Click!;

        _btnSelectCompressedFile = new Button();
        _btnSelectCompressedFile.Text = "Select Compressed";
        _btnSelectCompressedFile.Location = new Point(310, 60);
        _btnSelectCompressedFile.Size = new Size(150, 30);
        _btnSelectCompressedFile.Click += btnSelectCompressedFile_Click!;

        // Input Path Display
        var lblInputPath = new Label();
        lblInputPath.Text = "Selected Path:";
        lblInputPath.Location = new Point(20, 100);
        lblInputPath.Size = new Size(100, 23);
        lblInputPath.TextAlign = ContentAlignment.MiddleLeft;

        _txtInputPath = new TextBox();
        _txtInputPath.Location = new Point(130, 100);
        _txtInputPath.Size = new Size(570, 23);
        _txtInputPath.ReadOnly = true;
        _txtInputPath.BackColor = SystemColors.Control;

        // Action Buttons
        _btnCompress = new Button();
        _btnCompress.Text = "Compress";
        _btnCompress.Location = new Point(20, 140);
        _btnCompress.Size = new Size(80, 35);
        _btnCompress.UseVisualStyleBackColor = true;
        _btnCompress.Click += btnCompress_Click!;

        _btnDecompress = new Button();
        _btnDecompress.Text = "Decompress";
        _btnDecompress.Location = new Point(110, 140);
        _btnDecompress.Size = new Size(90, 35);
        _btnDecompress.UseVisualStyleBackColor = true;
        _btnDecompress.Click += btnDecompress_Click!;

        _btnExtractSingle = new Button();
        _btnExtractSingle.Text = "Extract Single";
        _btnExtractSingle.Location = new Point(210, 140);
        _btnExtractSingle.Size = new Size(150, 35);
        _btnExtractSingle.UseVisualStyleBackColor = true;
        _btnExtractSingle.Click += btnExtractSingle_Click!;
        _btnExtractSingle.Visible = false;

        // Control Buttons
        _btnPause = new Button();
        _btnPause.Text = "Pause";
        _btnPause.Location = new Point(450, 140);
        _btnPause.Size = new Size(70, 35);
        _btnPause.UseVisualStyleBackColor = true;
        _btnPause.Click += btnPause_Click!;

        _btnResume = new Button();
        _btnResume.Text = "Resume";
        _btnResume.Location = new Point(530, 140);
        _btnResume.Size = new Size(70, 35);
        _btnResume.UseVisualStyleBackColor = true;
        _btnResume.Click += btnResume_Click!;

        _btnCancel = new Button();
        _btnCancel.Text = "Cancel";
        _btnCancel.Location = new Point(610, 140);
        _btnCancel.Size = new Size(70, 35);
        _btnCancel.UseVisualStyleBackColor = true;
        _btnCancel.Click += btnCancel_Click!;

        // Progress Bars and Labels
        _lblHuffmanProgress = new Label();
        _lblHuffmanProgress.Text = "Huffman: 0%";
        _lblHuffmanProgress.Location = new Point(20, 190);
        _lblHuffmanProgress.Size = new Size(150, 20);
        _lblHuffmanProgress.TextAlign = ContentAlignment.MiddleLeft;

        _progressBarHuffman = new ProgressBar();
        _progressBarHuffman.Location = new Point(180, 190);
        _progressBarHuffman.Size = new Size(200, 20);
        _progressBarHuffman.Maximum = 100;
        _progressBarHuffman.Style = ProgressBarStyle.Continuous;

        _lblShannonProgress = new Label();
        _lblShannonProgress.Text = "Shannon-Fano: 0%";
        _lblShannonProgress.Location = new Point(20, 220);
        _lblShannonProgress.Size = new Size(150, 20);
        _lblShannonProgress.TextAlign = ContentAlignment.MiddleLeft;

        _progressBarShannon = new ProgressBar();
        _progressBarShannon.Location = new Point(180, 220);
        _progressBarShannon.Size = new Size(200, 20);
        _progressBarShannon.Maximum = 100;
        _progressBarShannon.Style = ProgressBarStyle.Continuous;

        // Results Section
        var lblResults = new Label();
        lblResults.Text = "Results:";
        lblResults.Location = new Point(20, 280);
        lblResults.Size = new Size(100, 23);

        _listBoxResults = new ListBox();
        _listBoxResults.Location = new Point(20, 310);
        _listBoxResults.Size = new Size(420, 320);
        _listBoxResults.HorizontalScrollbar = true;

        // Archive Files Section

        _listBoxArchiveFiles = new ListBox();
        _listBoxArchiveFiles.Location = new Point(460, 310);
        _listBoxArchiveFiles.Size = new Size(400, 320);
        _listBoxArchiveFiles.HorizontalScrollbar = true;

        Controls.AddRange(_btnSelectSingleFile, _btnSelectMultipleFiles, _btnSelectCompressedFile,
            _txtInputPath, _txtInputPassword, lblAlgorithm, _comboBoxAlgorithm, _btnCompress, _btnDecompress,
            _btnExtractSingle, _btnPause, _btnResume, _btnCancel, _listBoxResults, _listBoxArchiveFiles,
            _progressBarHuffman, _progressBarShannon, _lblHuffmanProgress, _lblShannonProgress);

        ResumeLayout(false);
    }
}