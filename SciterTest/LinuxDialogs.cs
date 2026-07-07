using System;
using System.Diagnostics;

public static class LinuxDialogs
{
    public static string ZenitySelectFile(string title, bool isFolder)
    {
        try
        {
            string args = $"--file-selection --title=\"{title}\"";
            if (isFolder) args += " --directory";

            var startInfo = new ProcessStartInfo
            {
                FileName = "zenity",
                Arguments = args,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null) 
                return string.Empty;

            string result = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit();

            return process.ExitCode == 0 ? result : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }
}
