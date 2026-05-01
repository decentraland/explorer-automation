namespace ExplorerAutomation.Tests.Common;

public static class Reporter
{
    private static readonly Dictionary<string, string> _unityLogs = new();
    
    public static void Log(string message)
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        var formattedMessage = $"[{timestamp}] {message}";

        TestContext.Progress.WriteLine(formattedMessage);
        AllureApi.Step(message);
    }

    public static void TakeScreenshot(string customName = null)
    {
        if (CommonStuff.AltDriver == null)
        {
            Log("Cannot take screenshot: AltDriver not set");
            return;
        }

        try
        {
            var projectDirectory = Directory.GetCurrentDirectory();
            var screenshotDirectory = Path.Combine(projectDirectory, "screenshots");

            // Create directory if it doesn't exist
            if (!Directory.Exists(screenshotDirectory))
            {
                Directory.CreateDirectory(screenshotDirectory);
            }

            var timestamp = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds();
            var fileName = customName ?? $"screenshot_{timestamp}";
            var screenshotPath = Path.Combine(screenshotDirectory, $"{fileName}.png");

            CommonStuff.AltDriver.GetPNGScreenshot(screenshotPath);
            AllureApi.Step($"Screenshot taken: {fileName}",
                () => { AllureApi.AddAttachment(name: fileName, content: File.ReadAllBytes(screenshotPath), type: "image/png"); });
        }
        catch (Exception ex)
        {
            Log($"Failed to take screenshot: {ex.Message}");
        }
    }

    public static void AttachFileToAllure(string filePath, string customName = null)
    {
        var fileName = customName ?? Path.GetFileNameWithoutExtension(filePath);

        AllureApi.Step($"Attach file: {fileName}", () =>
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    Log($"Cannot attach file: File not found at {filePath}");
                    return;
                }

                var fileExtension = Path.GetExtension(filePath).ToLower();

                // Determine content type based on file extension
                string contentType = fileExtension switch
                {
                    ".txt" or ".log" => "text/plain",
                    ".json"          => "application/json",
                    ".xml"           => "application/xml",
                    ".html"          => "text/html",
                    ".csv"           => "text/csv",
                    _                => "application/octet-stream"
                };

                AllureApi.AddAttachment(name: fileName, content: File.ReadAllBytes(filePath), type: contentType);
                Log($"File attached to Allure report: {fileName}");
            }
            catch (Exception ex)
            {
                Log($"Failed to attach file to Allure: {ex.Message}");
            }
        });
    }

    [AllureBefore("Setup Unity log listener")]
    public static void SetupUnityLogListener()
    {
        if (CommonStuff.AltDriver != null)
        {
            Reporter.Log("Setting up Unity log listener");
            CommonStuff.AltDriver.AddNotificationListener<AltLogNotificationResultParams>(
                NotificationType.LOG,
                LogCallback,
                true
            );
        }
    }

    private static void LogCallback(AltLogNotificationResultParams logParams)
    {
        var projectDirectory = Directory.GetCurrentDirectory();
        var logDirectory = Path.Combine(projectDirectory, "screenshots");

        if (!Directory.Exists(logDirectory))
            Directory.CreateDirectory(logDirectory);

        var testName = TestContext.CurrentContext.Test.Name;
        var filename = testName + "-UnityLogs.txt";
        var filepath = Path.Combine(logDirectory, filename);

        var log = logParams;

        using (var sw = new StreamWriter(filepath, true))
        {
            sw.WriteLine($"{log.message}");
            sw.WriteLine($"StackTrace : {log.stackTrace}");
            sw.WriteLine(log);
        }

        _unityLogs.TryAdd(filename, filepath);
    }

    [AllureAfter("Attach Unity logs to Allure report")]
    public static void AddUnityLogsToAllure()
    {
        foreach (var item in _unityLogs)
        {
            var attachmentName = TestContext.CurrentContext.Test.Name + "-" + item.Key;
            try
            {
                Reporter.AttachFileToAllure(item.Value, attachmentName);
            }
            catch (Exception)
            {
                Reporter.Log("No Unity logs found.");
            }

            _unityLogs.Remove(item.Key);
        }
    }
}