using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using UKSF.Api.Shared.Context;

namespace UKSF.Api.Shared.Queries
{
    public interface IGetEmailTemplateQuery
    {
        Task<string> ExecuteAsync(GetEmailTemplateQueryArgs args);
    }

    public class GetEmailTemplateQueryArgs
    {
        public GetEmailTemplateQueryArgs(string templateName, Dictionary<string, string> substitutions)
        {
            TemplateName = templateName;
            Substitutions = substitutions;
        }

        public string TemplateName { get; }
        public Dictionary<string, string> Substitutions { get; }
    }

    public class GetEmailTemplateQuery : IGetEmailTemplateQuery
    {
        private readonly IFileContext _fileContext;
        private readonly ConcurrentDictionary<string, string> _templateCache = new();

        public GetEmailTemplateQuery(IFileContext fileContext)
        {
            _fileContext = fileContext;
        }

        public async Task<string> ExecuteAsync(GetEmailTemplateQueryArgs args)
        {
            if (!_templateCache.TryGetValue(args.TemplateName, out var templateContent))
            {
                templateContent = await GetTemplateContent(args.TemplateName);
                _templateCache[args.TemplateName] = templateContent;
            }

            args.Substitutions.Add("randomness", DateTime.UtcNow.ToString(CultureInfo.InvariantCulture));

            return args.Substitutions.Aggregate(templateContent, (current, substitution) => current.Replace($"${substitution.Key}$", substitution.Value));
        }

        private async Task<string> GetTemplateContent(string templateName)
        {
            var templatePath = _fileContext.AppDirectory + $"/EmailHtmlTemplates/Premailed/{templateName}TemplatePremailed.html";

            if (!_fileContext.Exists(templatePath))
            {
                throw new ArgumentException($"Cannot find an email template named {templateName}");
            }

            return await _fileContext.ReadAllText(templatePath);
        }
    }

    public class TemplatedEmailNames
    {
        public static string ResetPasswordTemplate = "ResetPassword";
        public static string AccountConfirmationTemplate = "AccountConfirmation";
        public static string NotificationTemplate = "Notification";
    }
}
