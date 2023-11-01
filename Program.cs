
namespace KAPI.Launcher;

internal class Program
{
    private static readonly HttpClient httpClient = new();

    private static readonly string MainfestPath = Path.GetFullPath(Path.Combine("Roblox", "AppxManifest.xml")); // Windows App Manifest location
    private static readonly string RobloxPath = Path.GetFullPath("Roblox"); // Kapi Client install Location
    private static readonly string Version = Path.GetFullPath(Path.Combine("Roblox", "Client.xml")); // Hashed Client version location
    private static readonly string Aumid = "ROBLOXCORPORATION.KAPI-Client_55nm5eh3cm0pr!App"; // Aumid used to start the Client via the launcher
    private static readonly string appPackagePath = "ROBLOXCORPORATION.KAPI-Client_55nm5eh3cm0pr"; // Windows app package path, used to uninstall Client

    static async Task Main() // Main Task startup
    {
        Console.WriteLine("[KAPI Launcher] Checking Installation");
        string exeDirectory = AppDomain.CurrentDomain.BaseDirectory; // Set current directory to current local directory
        Directory.SetCurrentDirectory(exeDirectory);
        Console.Title = " [KAPI Launcher] ";

        await CloseOtherWindows10UniversalProcesses(Path.GetFullPath("Roblox\\Windows10Universal.exe")); // Close out all other Uwp Roblox proccesses that are not Kapi Client

        if (!Directory.Exists(RobloxPath)) await Install(); // If Client is not installed, install Kapi Client

        if (await CheckUpdate()) // Check for updated Client
        {
            await UninstallClient(appPackagePath); // Uninstall current Client if update is needed
            await Install(); // Reinstall
        }

        static async Task<bool> Install() // Install Client Task called if Client needs to be installed
        {
            Console.WriteLine("[KAPI Launcher] Installing KAPI Client\n[KAPI Launcher] Downloading Files Please Wait");
            await DownloadRoblox((string)(await GetLatestData())["KAPI-ClientDownload"], Path.Combine(RobloxPath, "Roblox.zip"));
            await InstallRoblox();
            return true;
        }

        Console.WriteLine("[KAPI Launcher] Launching KAPI Modified Roblox Client");
        await Task.Delay(1500);
        LaunchUWPApp(Aumid); // Launch Kapi Client
        // Exit Launcher after Client launched
    }
    private static async Task<bool> CheckUpdate()
    {
        try
        {
            if (!File.Exists(Version)) return true;

            string latestVersion = HashSHA256((string)(await GetLatestData())["ClientVersion"]);

            using var reader = new StreamReader(Version);
            string installedVersion = await reader.ReadToEndAsync();

            return !latestVersion.Contains(installedVersion);
        }
        catch (Exception ex)
        {
            await HandleError(ex);
            return true;
        }
    }
    private static async Task CloseOtherWindows10UniversalProcesses(string allowedProcessPath)
    {
        foreach (Process process in Process.GetProcessesByName("Windows10Universal"))
        {
            try
            {
                string processPath = process.MainModule.FileName;
                if (!processPath.Equals(allowedProcessPath, StringComparison.OrdinalIgnoreCase))
                {
                    process.Kill();
                }
            }
            catch (Exception ex)
            {
                await HandleError(ex);
            }
        }
    }
    private static async Task UninstallClient(string appPackagePath)
    {
        try
        {
            Console.WriteLine("[KAPI Launcher] Update Detected\n[KAPI Launcher] Uninstalling KAPI Client");
            await EnableDevMode(true);
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "powershell",
                    Arguments = $"-Command Remove-AppxPackage -Package {appPackagePath}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            process.Start();
            await process.WaitForExitAsync();
            await EnableDevMode(false);
        }
        catch (Exception ex)
        {
            await HandleError(ex);
        }
    }

    static async void LaunchUWPApp(string aumid)
    {
        try
        {
            Process.Start("explorer.exe", $"shell:AppsFolder\\{aumid}");
        }
        catch (Exception ex)
        {
            await HandleError(ex);
        }
    }

    private static async Task EnableDevMode(bool enable)
    {
        try
        {
            RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock", true);
            if (key != null)
            {
                key.SetValue("AllowDevelopmentWithoutDevLicense", enable ? 1 : 0, RegistryValueKind.DWord);
                key.Close();
                return;
            }
            Console.WriteLine($"An error occurred Settings Developer Mode");
            await Task.Delay(3000);
            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            await HandleError(ex);
        }
    }

    private static async Task InstallRoblox()
    {
        try
        {
            Console.WriteLine("[KAPI Launcher] Setting Up Files");
            ZipFile.ExtractToDirectory(Path.Combine(RobloxPath, "Roblox.zip"), RobloxPath);
            File.Delete(Path.Combine(RobloxPath, "Roblox.zip"));

            Console.WriteLine("[KAPI Launcher] Enabling Developer Settings");
            await EnableDevMode(true);

            Console.WriteLine("[KAPI Launcher] Installing KAPI Modified Roblox Client");
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "powershell",
                    Arguments = $"-Command Add-AppxPackage -path {MainfestPath} -register",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            process.Start();
            await process.WaitForExitAsync();

            Console.WriteLine("[KAPI Launcher] Disabling Developer Settings");
            await EnableDevMode(false);
        }
        catch (Exception ex)
        {
            await HandleError(ex);
        }
    }

    private static async Task DownloadRoblox(string endpoint, string destination)
    {
        try
        {
            if (Directory.Exists(RobloxPath)) Directory.Delete(RobloxPath, true);
            Directory.CreateDirectory("Roblox");

            using var response = await httpClient.GetAsync(endpoint, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            long? totalLength = response.Content.Headers.ContentLength;

            using var fileStream = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true);
            using var contentStream = await response.Content.ReadAsStreamAsync();
            var totalRead = 0L;
            var buffer = new byte[4096];
            int read;

            while ((read = await contentStream.ReadAsync(buffer)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, read));
                totalRead += read;
                if (totalLength.HasValue)
                {
                    double percentage = (double)totalRead / totalLength.Value * 100;
                    Console.Write($"\r[KAPI Launcher] Downloaded {percentage:F2}%     ");
                }
            }
            Console.WriteLine();
        }
        catch (HttpRequestException ex)
        {
            await HandleError(ex);
        }
    }

    private static JObject? latestDataCache;
    private static async Task<JObject> GetLatestData()
    {
        if (latestDataCache == null)
        {
            string text = await ReadURL("https://raw.githubusercontent.com/Dev-Nitro/KronosUwpFiles/main/KronosData.json");
            latestDataCache = JObject.Parse(text);
        }
        return latestDataCache;
    }

    private static async Task<string> ReadURL(string url)
    {
        try
        {
            HttpResponseMessage response = await httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"Error while fetching content from the URL: {ex.Message}");
            return string.Empty;
        }
    }
    private static string HashSHA256(string input)
    {
        byte[] data = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        StringBuilder stringBuilder = new();
        for (int i = 0; i < data.Length; i++)
        {
            stringBuilder.Append(data[i].ToString("x2"));
        }
        return stringBuilder.ToString();
    }
    private static async Task<bool> HandleError(Exception ex)
    {
        Console.WriteLine($"[KAPI Launcher] A fatal error occurred: {ex.Message}");
        Console.WriteLine("Press any key to exit...");
        Console.ReadKey();
        await Task.Delay(100);
        Environment.Exit(0);
        return true;
    }
}
