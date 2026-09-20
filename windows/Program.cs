using System.Diagnostics;
using System.Text;

namespace ipatoolUI.Windows;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}

internal sealed class MainForm : Form
{
    private readonly TextBox executablePath = new() { Dock = DockStyle.Fill };
    private readonly TextBox bundleId = new() { Dock = DockStyle.Fill, PlaceholderText = "com.example.app" };
    private readonly TextBox outputFolder = new() { Dock = DockStyle.Fill };
    private readonly TextBox output = new()
    {
        Dock = DockStyle.Fill,
        Multiline = true,
        ReadOnly = true,
        ScrollBars = ScrollBars.Both,
        WordWrap = false,
        BackColor = SystemColors.Window
    };
    private readonly ProgressBar progress = new() { Dock = DockStyle.Fill, Style = ProgressBarStyle.Marquee, Visible = false };
    private readonly Label status = new() { Text = "Ready", AutoSize = true };
    private readonly Button downloadButton = new() { Text = "Download IPA", AutoSize = true };
    private Process? runningProcess;

    public MainForm()
    {
        Text = "ipatoolUI - Windows IPA Downloader";
        MinimumSize = new Size(820, 560);
        StartPosition = FormStartPosition.CenterScreen;

        var browseExecutableButton = new Button { Text = "Browse…", AutoSize = true };
        browseExecutableButton.Click += (_, _) => BrowseForExecutable();

        var browseOutputButton = new Button { Text = "Choose folder…", AutoSize = true };
        browseOutputButton.Click += (_, _) => BrowseForOutputFolder();

        downloadButton.Click += async (_, _) => await DownloadIpaAsync();

        var form = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            ColumnCount = 1,
            RowCount = 8
        };
        form.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        form.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        form.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        form.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        form.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        form.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        form.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        form.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        form.Controls.Add(new Label { Text = "ipatool executable:", AutoSize = true }, 0, 0);
        form.Controls.Add(CreateRow(executablePath, browseExecutableButton), 0, 1);
        form.Controls.Add(new Label { Text = "App Store bundle ID:", AutoSize = true, Padding = new Padding(0, 8, 0, 0) }, 0, 2);
        form.Controls.Add(bundleId, 0, 3);
        form.Controls.Add(new Label { Text = "IPA output folder:", AutoSize = true, Padding = new Padding(0, 8, 0, 0) }, 0, 4);
        form.Controls.Add(CreateRow(outputFolder, browseOutputButton), 0, 5);
        form.Controls.Add(CreateActionRow(), 0, 6);
        form.Controls.Add(output, 0, 7);

        var container = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2 };
        container.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        container.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        container.Controls.Add(form, 0, 0);
        container.Controls.Add(output, 0, 1);
        Controls.Add(container);

        executablePath.Text = FindIpatool() ?? "ipatool.exe";
        outputFolder.Text = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "ipatoolUI");
        Directory.CreateDirectory(outputFolder.Text);
    }

    private Control CreateRow(Control editor, Control button)
    {
        var row = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        row.Controls.Add(editor, 0, 0);
        row.Controls.Add(button, 1, 0);
        return row;
    }

    private Control CreateActionRow()
    {
        var row = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3 };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        row.Controls.Add(downloadButton, 0, 0);
        row.Controls.Add(progress, 1, 0);
        row.Controls.Add(status, 2, 0);
        return row;
    }

    private void BrowseForExecutable()
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "Executables (*.exe)|*.exe|All files (*.*)|*.*",
            Title = "Select ipatool.exe"
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
            executablePath.Text = dialog.FileName;
    }

    private void BrowseForOutputFolder()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Choose where downloaded IPA files should be saved",
            SelectedPath = outputFolder.Text,
            UseDescriptionForTitle = true
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
            outputFolder.Text = dialog.SelectedPath;
    }

    private async Task DownloadIpaAsync()
    {
        var executable = executablePath.Text.Trim();
        var identifier = bundleId.Text.Trim();
        var destination = outputFolder.Text.Trim();

        if (string.IsNullOrWhiteSpace(executable) || !File.Exists(executable))
        {
            MessageBox.Show(this, "Select a valid ipatool.exe first.", "ipatoolUI", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (!IsBundleIdentifier(identifier))
        {
            MessageBox.Show(this, "Enter a valid bundle ID, for example com.example.app.", "ipatoolUI", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (string.IsNullOrWhiteSpace(destination))
        {
            MessageBox.Show(this, "Choose an output folder first.", "ipatoolUI", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        Directory.CreateDirectory(destination);
        output.Clear();
        SetBusy(true, "Downloading…");
        AppendOutput($"Downloading {identifier} to {destination}");

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = executable,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
            startInfo.ArgumentList.Add("download");
            startInfo.ArgumentList.Add("--bundle-identifier");
            startInfo.ArgumentList.Add(identifier);
            startInfo.ArgumentList.Add("--output");
            startInfo.ArgumentList.Add(destination);
            startInfo.ArgumentList.Add("--format");
            startInfo.ArgumentList.Add("json");

            using var process = new Process { StartInfo = startInfo };
            runningProcess = process;
            process.OutputDataReceived += (_, e) => AppendOutput(e.Data);
            process.ErrorDataReceived += (_, e) => AppendOutput(e.Data);
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            await process.WaitForExitAsync();
            AppendOutput($"\r\nExit code: {process.ExitCode}");
            SetStatus(process.ExitCode == 0 ? "Download complete" : "Download failed");
        }
        catch (Exception exception)
        {
            AppendOutput($"Error: {exception.Message}");
            SetStatus("Download failed");
        }
        finally
        {
            runningProcess = null;
            SetBusy(false, status.Text);
        }
    }

    private static bool IsBundleIdentifier(string value)
    {
        var parts = value.Split('.', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 && parts.All(part => part.All(character => char.IsLetterOrDigit(character) || character == '-' || character == '_'));
    }

    private void SetBusy(bool busy, string message)
    {
        downloadButton.Enabled = !busy;
        progress.Visible = busy;
        SetStatus(message);
    }

    private void SetStatus(string message)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => SetStatus(message));
            return;
        }
        status.Text = message;
    }

    private void AppendOutput(string? line)
    {
        if (line is null) return;
        if (InvokeRequired)
        {
            BeginInvoke(() => AppendOutput(line));
            return;
        }
        output.AppendText(line + Environment.NewLine);
    }

    private static string? FindIpatool()
    {
        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        return path.Split(Path.PathSeparator)
            .Select(directory => Path.Combine(directory, "ipatool.exe"))
            .FirstOrDefault(File.Exists);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (runningProcess is { HasExited: false })
            runningProcess.Kill(entireProcessTree: true);
        base.OnFormClosing(e);
    }
}
