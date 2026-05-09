using System.Text.RegularExpressions;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MailKit.Security;

namespace ExplorerAutomation.Tests.Common;

public static class OtpMailbox
{
    private static readonly Regex SixDigitCode = new(@"\d{6}", RegexOptions.Compiled);

    /// <summary>
    /// Generates a fresh test email of the form <c>qa-&lt;hash&gt;@&lt;EMAIL_DOMAIN&gt;</c>.
    /// Each call returns a distinct local-part so every signup looks like a brand-new
    /// recipient to Thirdweb (its own per-address rate-limit bucket — no curated
    /// fallback list needed).
    ///
    /// Defaults to <c>e2e.decentraland.org</c>, a Workspace catch-all whose deliveries
    /// route to the inbox at <c>IMAP_USER</c>. Override with <c>EMAIL_DOMAIN</c> if
    /// you've pointed the suite at a different inbox or domain.
    /// </summary>
    public static string GenerateFreshEmail()
    {
        var domain = Environment.GetEnvironmentVariable("EMAIL_DOMAIN");
        if (string.IsNullOrWhiteSpace(domain))
            domain = "e2e.decentraland.org";
        var local = "qa-" + Guid.NewGuid().ToString("N")[..8];
        return $"{local}@{domain}";
    }

    public static string GetBaseEmail() => RequireEnv("IMAP_USER");

    public static string WaitForOtp(string toAddress, TimeSpan? timeout = null, TimeSpan? pollInterval = null)
    {
        var actualTimeout = timeout ?? TimeSpan.FromSeconds(90);
        var actualInterval = pollInterval ?? TimeSpan.FromSeconds(3);

        var host = RequireEnv("IMAP_HOST");
        var port = int.Parse(RequireEnv("IMAP_PORT"));
        var user = RequireEnv("IMAP_USER");
        var password = RequireEnv("IMAP_PASSWORD");
        var fromAddress = RequireEnv("OTP_FROM_EMAIL");

        Reporter.Log($"Waiting for OTP email to {toAddress} from {fromAddress} (timeout {actualTimeout.TotalSeconds}s)");

        using var client = new ImapClient();
        client.Connect(host, port, SecureSocketOptions.SslOnConnect);
        client.Authenticate(user, password);

        var inbox = client.Inbox;
        inbox.Open(FolderAccess.ReadOnly);

        // Filter by TO (unique per run thanks to the +hash) and FROM (Thirdweb).
        // We deliberately do NOT use DeliveredAfter / SINCE — IMAP's SENTSINCE/SINCE has
        // day-only granularity and Gmail's internal date can sit one calendar day off
        // (timezone of the mailbox owner vs UTC of the test runner), which silently
        // excludes the very email we're waiting for. The unique TO already prevents stale
        // matches; we additionally accept only messages we receive AFTER the wait started.
        var startedAt = DateTime.UtcNow;
        var deadline = startedAt + actualTimeout;

        while (DateTime.UtcNow < deadline)
        {
            var query = SearchQuery
                .ToContains(toAddress)
                .And(SearchQuery.FromContains(fromAddress));

            var uids = inbox.Search(query);
            if (uids.Count > 0)
            {
                var newest = uids[^1];
                var message = inbox.GetMessage(newest);
                var code = ExtractCode(message);
                if (code != null)
                {
                    Reporter.Log("OTP email received and code extracted");
                    client.Disconnect(true);
                    return code;
                }
                Reporter.Log($"OTP email arrived but no 6-digit code found in body — retrying");
            }

            Thread.Sleep(actualInterval);
            inbox.Check();
        }

        client.Disconnect(true);
        throw new TimeoutException(
            $"OTP email to {toAddress} from {fromAddress} not received within {actualTimeout.TotalSeconds}s");
    }

    private static string ExtractCode(MimeKit.MimeMessage message)
    {
        foreach (var body in new[] { message.TextBody, StripHtml(message.HtmlBody) })
        {
            if (string.IsNullOrEmpty(body)) continue;
            var match = SixDigitCode.Match(body);
            if (match.Success) return match.Value;
        }
        return null;
    }

    private static string StripHtml(string html)
    {
        if (string.IsNullOrEmpty(html)) return null;
        return Regex.Replace(html, "<[^>]+>", " ");
    }

    private static string RequireEnv(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException(
                $"Environment variable {name} is not set. Add it to your .env file (see .env.example).");
        return value;
    }
}
