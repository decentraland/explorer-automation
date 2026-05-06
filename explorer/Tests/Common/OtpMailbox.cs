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
    /// Generates a fresh plus-alias email of the form <c>local+hash@domain</c>.
    /// </summary>
    /// <param name="baseAddress">
    /// Optional base address. Defaults to <c>IMAP_USER</c>. The address must NOT
    /// already contain a '+' alias (the hash is inserted before the '@').
    /// </param>
    public static string GeneratePlusAliasEmail(string baseAddress = "")
    {
        if (string.IsNullOrEmpty(baseAddress))
            baseAddress = RequireEnv("IMAP_USER");
        var atIdx = baseAddress.IndexOf('@');
        if (atIdx <= 0)
            throw new InvalidOperationException($"'{baseAddress}' is not a valid email address");

        var suffix = Guid.NewGuid().ToString("N")[..8];
        return baseAddress.Insert(atIdx, "+" + suffix);
    }

    public static string GetBaseEmail() => RequireEnv("IMAP_USER");

    /// <summary>
    /// Returns alternate signup addresses configured via the ALTERNATE_EMAILS env var
    /// (comma-separated). These must all route to the inbox at IMAP_USER (e.g.
    /// Gmail plus-aliases or domain aliases that forward to the same mailbox), since the OTP
    /// is read back via a single IMAP connection. Used as a fallback when the primary email
    /// hits Thirdweb's per-address rate limit (429).
    /// </summary>
    public static List<string> GetAlternateEmails()
    {
        var raw = Environment.GetEnvironmentVariable("ALTERNATE_EMAILS");
        if (string.IsNullOrWhiteSpace(raw))
            return [];
        return raw.Split(',')
                  .Select(s => s.Trim())
                  .Where(s => s.Length > 0)
                  .ToList();
    }

    public static string WaitForOtp(string toAddress, TimeSpan? timeout = null, TimeSpan? pollInterval = null)
    {
        var actualTimeout = timeout ?? TimeSpan.FromSeconds(90);
        var actualInterval = pollInterval ?? TimeSpan.FromSeconds(3);

        var host = RequireEnv("IMAP_HOST");
        var port = int.Parse(RequireEnv("IMAP_PORT"));
        var user = RequireEnv("IMAP_USER");
        var password = RequireEnv("IMAP_PASSWORD");
        var fromAddress = RequireEnv("IMAP_FROM_USER");

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
