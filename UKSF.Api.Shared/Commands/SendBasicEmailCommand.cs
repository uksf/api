using System.Net.Mail;
using UKSF.Api.Shared.Context;

namespace UKSF.Api.Shared.Commands;

public interface ISendBasicEmailCommand
{
    Task ExecuteAsync(SendBasicEmailCommandArgs args);
}

public class SendBasicEmailCommandArgs
{
    public SendBasicEmailCommandArgs(string recipient, string subject, string body)
    {
        Recipient = recipient;
        Subject = subject;
        Body = body;
    }

    public string Recipient { get; }
    public string Subject { get; }
    public string Body { get; }
}

public class SendBasicEmailCommand : ISendBasicEmailCommand
{
    private readonly ISmtpClientContext _smtpClientContext;

    public SendBasicEmailCommand(ISmtpClientContext smtpClientContext)
    {
        _smtpClientContext = smtpClientContext;
    }

    public async Task ExecuteAsync(SendBasicEmailCommandArgs args)
    {
        using MailMessage mail = new();
        mail.To.Add(args.Recipient);
        mail.Subject = args.Subject;
        mail.Body = args.Body;
        mail.IsBodyHtml = true;

        await _smtpClientContext.SendEmailAsync(mail);
    }
}
