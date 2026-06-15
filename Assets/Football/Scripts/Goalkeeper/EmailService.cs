using System;
using System.Net;
using System.Net.Mail;
using System.Net.Security;
using System.Threading;
using UnityEngine;

/// <summary>
/// Sends the player's result by email via SMTP. Configure in the inspector.
/// For Gmail: Host = smtp.gmail.com, Port = 587, SSL on, From = your gmail,
/// Password = a Google "App Password" (not your normal password; requires 2FA).
///
/// SECURITY: the password is stored on this component (scene asset). Fine for a
/// local/demo build, but do NOT commit/ship it publicly. For production use a backend.
/// </summary>
public class EmailService : MonoBehaviour
{
    [Header("Enable")]
    public bool sendEnabled = true;

    [Header("SMTP settings")]
    public string smtpHost = "smtp.gmail.com";
    public int smtpPort = 587;
    public bool enableSsl = true;
    public string fromAddress = "";
    [Tooltip("SMTP password / Gmail App Password. Stored in the scene — keep private.")]
    public string password = "";
    public string fromDisplayName = "Goalkeeper Challenge";

    public bool IsConfigured =>
        !string.IsNullOrEmpty(smtpHost) && !string.IsNullOrEmpty(fromAddress) && !string.IsNullOrEmpty(password);

    public static bool LooksLikeEmail(string e)
    {
        if (string.IsNullOrWhiteSpace(e)) return false;
        int at = e.IndexOf('@');
        return at > 0 && e.IndexOf('.', at) > at + 1 && !e.EndsWith(".");
    }

    /// <summary>Composes and sends the result email on a background thread (SMTP blocks).</summary>
    public void SendResult(string playerName, string toEmail, int saves, int shots)
    {
        if (!sendEnabled) return;
        if (!LooksLikeEmail(toEmail)) { Debug.LogWarning("[Email] No valid recipient address; skipping."); return; }
        if (!IsConfigured) { Debug.LogWarning("[Email] SMTP not configured (set From + Password on the EmailService). Skipping send."); return; }

        string subject = "Your Goalkeeper Challenge result ⚽";
        string body =
            $"Hi {playerName},\n\n" +
            $"Thanks for playing the Goalkeeper Challenge!\n\n" +
            $"Your result: {saves} save(s) from {shots} shot(s).\n\n" +
            $"Come back and beat your score!\n";

        // capture values for the worker thread
        string host = smtpHost, from = fromAddress, pass = password, disp = fromDisplayName;
        int port = smtpPort; bool ssl = enableSsl;

        var t = new Thread(() =>
        {
            try
            {
                ServicePointManager.ServerCertificateValidationCallback =
                    (RemoteCertificateValidationCallback)((s, c, ch, e) => true); // pragmatic for Mono TLS
                using (var msg = new MailMessage())
                {
                    msg.From = new MailAddress(from, disp);
                    msg.To.Add(toEmail);
                    msg.Subject = subject;
                    msg.Body = body;
                    using (var client = new SmtpClient(host, port))
                    {
                        client.EnableSsl = ssl;
                        client.DeliveryMethod = SmtpDeliveryMethod.Network;
                        client.UseDefaultCredentials = false;
                        client.Credentials = new NetworkCredential(from, pass);
                        client.Send(msg);
                    }
                }
                Debug.Log("[Email] Result sent to " + toEmail);
            }
            catch (Exception e) { Debug.LogWarning("[Email] Send failed: " + e.Message); }
        });
        t.IsBackground = true;
        t.Start();
    }
}
