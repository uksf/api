using System.Net.Mail;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Queries;

namespace UKSF.Api.Core.Commands;

public interface ISendTemplatedEmailCommand
{
    Task ExecuteAsync(SendTemplatedEmailCommandArgs args);
}

public class SendTemplatedEmailCommandArgs
{
    public SendTemplatedEmailCommandArgs(string recipient, string subject, string templateName, Dictionary<string, string> substitutions)
    {
        Recipient = recipient;
        Subject = subject;
        TemplateName = templateName;
        Substitutions = substitutions;
    }

    public string Recipient { get; }
    public string Subject { get; }
    public string TemplateName { get; }
    public Dictionary<string, string> Substitutions { get; }
}

public class SendTemplatedEmailCommand : ISendTemplatedEmailCommand
{
    private readonly IGetEmailTemplateQuery _getEmailTemplateQuery;
    private readonly ISmtpClientContext _smtpClientContext;

    public SendTemplatedEmailCommand(IGetEmailTemplateQuery getEmailTemplateQuery, ISmtpClientContext smtpClientContext)
    {
        _getEmailTemplateQuery = getEmailTemplateQuery;
        _smtpClientContext = smtpClientContext;
    }

    public async Task ExecuteAsync(SendTemplatedEmailCommandArgs args)
    {
        using MailMessage mail = new();
        mail.To.Add(args.Recipient);
        mail.Subject = args.Subject;
        mail.Body = await _getEmailTemplateQuery.ExecuteAsync(new GetEmailTemplateQueryArgs(args.TemplateName, args.Substitutions));
        mail.IsBodyHtml = true;

        await _smtpClientContext.SendEmailAsync(mail);
    }
}
