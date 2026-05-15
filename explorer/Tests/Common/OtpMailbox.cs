using System.Text.RegularExpressions;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MailKit.Security;
using MimeKit;

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
        // Trim + strip a stray leading '@' so a slightly-malformed value still
        // produces a valid recipient.
        domain = domain.Trim().TrimStart('@');
        var local = "qa-" + Guid.NewGuid().ToString("N")[..8];
        return $"{local}@{domain}";
    }

    public static string GetBaseEmail() => RequireEnv("IMAP_USER");

    /// <summary>
    /// Connect to IMAP and poll for an OTP email <b>explicitly addressed to</b>
    /// <paramref name="toAddress"/> from the configured Thirdweb sender. Returns the
    /// 6-digit code from the message body.
    ///
    /// <para>
    /// Recipient verification: filter the IMAP search by sender only and verify
    /// on every candidate that the recipient actually matches. Forwarders
    /// (Cloudflare Email Routing for <c>*@e2e.decentraland.org</c> in particular)
    /// don't always preserve the original <c>To:</c> header when delivering to
    /// <c>IMAP_USER</c>'s mailbox — some rewrite To, others stash the original
    /// in <c>Delivered-To</c> / <c>X-Original-To</c>, and a few only echo the
    /// recipient in the body. So we check all of: To / Cc / Bcc / Delivered-To
    /// / X-Original-To, with the message body as a final fallback.
    /// </para>
    ///
    /// <para>
    /// Stale-OTP guard: capture <c>startedAt</c> (with a 5s buffer to absorb the
    /// gap between triggering the OTP send and entering this method) and reject
    /// any message whose <c>InternalDate</c> is before that moment. Walks UIDs
    /// newest-first and bails at the first stale one (every UID below it must
    /// also be stale). Guards against picking up an OTP from a prior run that
    /// shares the same sender.
    /// </para>
    /// </summary>
    public static string WaitForOtp(string toAddress, TimeSpan? timeout = null, TimeSpan? pollInterval = null)
    {
        var startedAt = DateTime.UtcNow.AddSeconds(-5);
        var actualTimeout = timeout ?? TimeSpan.FromSeconds(90);
        var actualInterval = pollInterval ?? TimeSpan.FromSeconds(3);

        var host = RequireEnv("IMAP_HOST");
        var portStr = RequireEnv("IMAP_PORT");
        if (!int.TryParse(portStr, out var port) || port < 1 || port > 65535)
            throw new InvalidOperationException(
                $"IMAP_PORT must be a valid port number (1-65535), got \"{portStr}\"");
        var user = RequireEnv("IMAP_USER");
        var password = RequireEnv("IMAP_PASSWORD");
        var fromAddress = RequireEnv("OTP_FROM_EMAIL");

        Reporter.Log($"Waiting for OTP email to {toAddress} from {fromAddress} (timeout {actualTimeout.TotalSeconds}s)");

        using var client = new ImapClient();
        client.Connect(host, port, SecureSocketOptions.SslOnConnect);
        client.Authenticate(user, password);

        var inbox = client.Inbox;
        inbox.Open(FolderAccess.ReadOnly);

        var deadline = DateTime.UtcNow + actualTimeout;
        var expected = toAddress.Trim().ToLowerInvariant();

        while (DateTime.UtcNow < deadline)
        {
            // Sender-only IMAP filter — recipient verified post-fetch.
            var query = SearchQuery.FromContains(fromAddress);
            var uids = inbox.Search(query);

            if (uids.Count > 0)
            {
                var summaries = inbox.Fetch(uids, MessageSummaryItems.InternalDate);
                // summaries[i] corresponds to uids[i] (MailKit preserves order).
                // Walk newest-to-oldest; bail at first stale UID.
                for (var i = summaries.Count - 1; i >= 0; i--)
                {
                    var summary = summaries[i];
                    var internalDate = summary.InternalDate?.UtcDateTime ?? DateTime.MinValue;
                    if (internalDate < startedAt)
                    {
                        Reporter.Log("Reached stale UID — fresh OTP not yet delivered");
                        break;
                    }

                    var message = inbox.GetMessage(summary.UniqueId);
                    if (!EmailIsForRecipient(message, expected))
                    {
                        // From Thirdweb but not addressed to us — concurrent run's
                        // OTP, or noise. Skip and check older.
                        continue;
                    }

                    var code = ExtractCode(message);
                    if (code != null)
                    {
                        Reporter.Log($"OTP email received and code extracted (recipient verified for {toAddress})");
                        client.Disconnect(true);
                        return code;
                    }
                    Reporter.Log("Recipient matched but no 6-digit code found in body — checking older");
                }
            }

            Thread.Sleep(actualInterval);
            inbox.Check();
        }

        client.Disconnect(true);
        throw new TimeoutException(
            $"OTP email to {toAddress} from {fromAddress} not received within {actualTimeout.TotalSeconds}s");
    }

    /// <summary>
    /// True iff <paramref name="expectedLowercase"/> appears in any
    /// recipient-bearing header (To / Cc / Bcc / Delivered-To / X-Original-To)
    /// or — as a final fallback — in the message body. Case-insensitive
    /// substring match.
    /// </summary>
    private static bool EmailIsForRecipient(MimeMessage message, string expectedLowercase)
    {
        if (string.IsNullOrEmpty(expectedLowercase)) return false;

        var candidates = new List<string>();
        if (message.To != null && message.To.Count > 0) candidates.Add(message.To.ToString());
        if (message.Cc != null && message.Cc.Count > 0) candidates.Add(message.Cc.ToString());
        if (message.Bcc != null && message.Bcc.Count > 0) candidates.Add(message.Bcc.ToString());

        var deliveredTo = message.Headers["Delivered-To"];
        if (!string.IsNullOrEmpty(deliveredTo)) candidates.Add(deliveredTo);
        var xOriginalTo = message.Headers["X-Original-To"];
        if (!string.IsNullOrEmpty(xOriginalTo)) candidates.Add(xOriginalTo);

        foreach (var c in candidates)
        {
            if (c.ToLowerInvariant().Contains(expectedLowercase)) return true;
        }

        // Body fallback.
        var text = message.TextBody ?? string.Empty;
        var html = StripHtml(message.HtmlBody) ?? string.Empty;
        var body = (text + "\n" + html).ToLowerInvariant();
        return body.Contains(expectedLowercase);
    }

    private static string? ExtractCode(MimeMessage message)
    {
        foreach (var body in new[] { message.TextBody, StripHtml(message.HtmlBody) })
        {
            if (string.IsNullOrEmpty(body)) continue;
            var match = SixDigitCode.Match(body);
            if (match.Success) return match.Value;
        }
        return null;
    }

    private static string? StripHtml(string? html)
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
