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

    #region Verification screenshots

    private const int VERIFY_SHOT_MAX_WIDTH = 1200;
    private const int VERIFY_SHOT_JPEG_QUALITY = 60;

    // AltTester's screenShotQuality is a resolution percentage: 50 halves each dimension on
    // the Unity side, cutting the synchronous wire transfer + decode cost of every shot by
    // ~4x. The output is a <=1200px JPEG anyway, so a half-res capture of a 2.5K/4K frame
    // still exceeds the target width; smaller frames just skip the downscale.
    private const int VERIFY_SHOT_CAPTURE_QUALITY = 50;

    // Opt-out toggle for CI: VERIFY_SCREENSHOTS unset/anything-else => on,
    // "0"/"false"/"off"/"no" (case-insensitive) => off.
    private static readonly bool _verificationShotsEnabled = IsVerifyScreenshotsEnabled();

    // Armed only between BaseTest.SetUp and BaseTest.TearDown so fixture plumbing
    // (EnsureInWorld boot waits, the pre-test auth-screen probe, teardown) stays shot-free.
    private static bool _verificationShotsArmed;

    // Per-test sequence so attachment names are deterministic and ordered; reset in SetUp.
    private static int _verificationShotSeq;

    private static bool IsVerifyScreenshotsEnabled()
    {
        var value = Environment.GetEnvironmentVariable("VERIFY_SCREENSHOTS")?.Trim();
        string[] disabledValues = ["0", "false", "off", "no"];
        return value is null || !disabledValues.Contains(value, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Arms verification shots and resets the per-test counter. Called from BaseTest.SetUp.</summary>
    public static void StartVerificationShots()
    {
        _verificationShotSeq = 0;
        _verificationShotsArmed = true;
    }

    /// <summary>Disarms verification shots. Called from BaseTest.TearDown before the final-frame screenshot.</summary>
    public static void StopVerificationShots()
    {
        _verificationShotsArmed = false;
    }

    /// <summary>
    /// Captures the current frame and attaches it to the current Allure step as a
    /// downscaled JPEG named "&lt;label&gt;_&lt;n&gt;". Intended to be called exactly once
    /// at the completion of a visual verification (element appeared/gone/present/text read) —
    /// never from inside polling loops and never for plain actions like Click/SetText.
    /// Capture failures are logged and swallowed so a broken screenshot path can't fail a test.
    /// </summary>
    public static void TakeVerificationShot(string label)
    {
        if (!_verificationShotsEnabled || !_verificationShotsArmed || CommonStuff.AltDriver == null)
            return;

        try
        {
            var attachmentName = $"{SanitizeShotLabel(label)}_{Interlocked.Increment(ref _verificationShotSeq)}";

            using var bmp = ScreenshotCapture.CaptureBitmap(quality: VERIFY_SHOT_CAPTURE_QUALITY);
            var jpegBytes = ScreenshotCapture.EncodeJpegScaled(bmp, VERIFY_SHOT_MAX_WIDTH, VERIFY_SHOT_JPEG_QUALITY);

            AllureApi.AddAttachment(name: attachmentName, type: "image/jpeg", content: jpegBytes, fileExtension: ".jpg");
        }
        catch (Exception ex)
        {
            Log($"Failed to take verification screenshot '{label}': {ex.Message}");
        }
    }

    private static string SanitizeShotLabel(string label)
    {
        var chars = label.Select(c => char.IsLetterOrDigit(c) || c is '_' or '-' or '.' ? c : '_').ToArray();
        var sanitized = new string(chars);
        return sanitized.Length <= 80 ? sanitized : sanitized[..80];
    }

    #endregion

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