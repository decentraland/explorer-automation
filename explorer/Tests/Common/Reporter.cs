using System.Collections.Concurrent;

namespace ExplorerAutomation.Tests.Common;

public static class Reporter
{
    // Written from the AltTester LOG notification thread (LogCallback) and read/drained from the
    // teardown thread (AddUnityLogsToAllure), so it must be concurrency-safe.
    private static readonly ConcurrentDictionary<string, string> _unityLogs = new();

    // Serializes the per-callback file appends — two notifications for the same test name can
    // otherwise race on the same file handle and throw IOException on the notification thread.
    private static readonly object _logFileLock = new();
    
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

            if (!Directory.Exists(screenshotDirectory))
            {
                Directory.CreateDirectory(screenshotDirectory);
            }

            var timestamp = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds();
            var fileName = customName ?? $"screenshot_{timestamp}";
            var screenshotPath = Path.Combine(screenshotDirectory, $"{fileName}.png");

            // Avoid AltDriver.GetPNGScreenshot — known StackOverflow on the .NET driver in 2.3.x.
            using var bmp = ScreenshotCapture.CaptureBitmap(quality: 100);
            var pngBytes = ScreenshotCapture.EncodePng(bmp);
            File.WriteAllBytes(screenshotPath, pngBytes);

            AllureApi.Step($"Screenshot taken: {fileName}", () => AttachPng(fileName, pngBytes));
        }
        catch (Exception ex)
        {
            Log($"Failed to take screenshot: {ex.Message}");
        }
    }

    public static void AttachPng(string name, byte[] pngBytes)
    {
        AllureApi.AddAttachment(name: name, type: "image/png", content: pngBytes);
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
        // Runs on AltTester's notification thread — an uncaught exception here can destabilize
        // the listener, so the whole body is guarded.
        try
        {
            var projectDirectory = Directory.GetCurrentDirectory();
            var logDirectory = Path.Combine(projectDirectory, "screenshots");

            if (!Directory.Exists(logDirectory))
                Directory.CreateDirectory(logDirectory);

            var testName = TestContext.CurrentContext.Test.Name;
            var filename = testName + "-UnityLogs.txt";
            var filepath = Path.Combine(logDirectory, filename);

            var log = logParams;

            lock (_logFileLock)
            {
                using var sw = new StreamWriter(filepath, true);
                sw.WriteLine($"{log.message}");
                sw.WriteLine($"StackTrace : {log.stackTrace}");
                sw.WriteLine(log);
            }

            _unityLogs.TryAdd(filename, filepath);
        }
        catch (Exception ex)
        {
            // Best-effort diagnostic only — the catch itself must not throw on the
            // notification thread, so guard the write too.
            try
            {
                TestContext.Progress.WriteLine($"Failed to persist Unity log notification: {ex.Message}");
            }
            catch
            {
                // Nothing safe left to do; swallow so the listener stays alive.
            }
        }
    }

    [AllureAfter("Attach Unity logs to Allure report")]
    public static void AddUnityLogsToAllure()
    {
        // Drain over a snapshot of the keys — removing from a dictionary while enumerating it
        // throws InvalidOperationException, which previously propagated out of OneTimeTearDown
        // and skipped StopDriver (leaking the AltDriver connection) on every run with Unity logs.
        foreach (var item in _unityLogs.ToArray())
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

            _unityLogs.TryRemove(item.Key, out _);
        }
    }
}