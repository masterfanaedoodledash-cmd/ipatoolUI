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
    private readonly TextBox arguments = new() { Dock = DockStyle.Fill };
    private readonly TextBox output = new()
    {
        Dock = DockStyle.Fill,
        Multiline = true,
        ReadOnly = true,
        ScrollBars = ScrollBars.Both,
        WordWrap = false
    };
    private readonly Button runButton = new() { Text = "Run ipatool", AutoSize = true };

    public MainForm()
    {
        Text = "ipatoolUI";
        MinimumSize = new Size(720, 480);
        StartPosition = FormStartPosition.CenterScreen;

        var browseButton = new Button { Text = "Browse…", AutoSize = true };
        browseButton.Click += (_, _) => BrowseForExecutable();
        runButton.Click += async (_, _) => await RunIpatoolAsync();

        var pathRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        pathRow.Controls.Add(executablePath, 0, 0);
        pathRow.Controls.Add(browseButton, 1, 0);

        var form = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), RowCount = 4 };
        form.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        form.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        form.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        form.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        form.Controls.Add(new Label { Text = "ipatool executable:", AutoSize = true }, 0, 0);
        form.Controls.Add(pathRow, 0, 1);
        form.Controls.Add(new Label { Text = "Arguments:", AutoSize = true, Padding = new Padding(0, 8, 0, 0) }, 0, 2);

        var argumentsRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        argumentsRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        argumentsRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        argumentsRow.Controls.Add(arguments, 0, 0);
        argumentsRow.Controls.Add(runButton, 1, 0);
        form.Controls.Add(argumentsRow, 0, 3);

        var container = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2 };
        container.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        container.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        container.Controls.Add(form, 0, 0);
        container.Controls.Add(output, 0, 1);
        Controls.Add(container);

        executablePath.Text = FindIpatool() ?? "ipatool.exe";
        arguments.Text = "--format json --help";
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

    private async Task RunIpatoolAsync()
    {
        if (string.IsNullOrWhiteSpace(executablePath.Text))
        {
            MessageBox.Show(this, "Select ipatool.exe first.", "ipatoolUI", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        runButton.Enabled = false;
        output.Clear();
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = executablePath.Text,
                Arguments = arguments.Text,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
            using var process = new Process { StartInfo = startInfo };
            process.OutputDataReceived += (_, e) => AppendOutput(e.Data);
            process.ErrorDataReceived += (_, e) => AppendOutput(e.Data);
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            await process.WaitForExitAsync();
            AppendOutput($"\r\nExit code: {process.ExitCode}");
        }
        catch (Exception exception)
        {
            AppendOutput($"Error: {exception.Message}");
        }
        finally
        {
            runButton.Enabled = true;
        }
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
}
