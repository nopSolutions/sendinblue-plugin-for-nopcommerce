using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.Misc.SendInBlue.Models
{
    public class SendInBlueModel
    {
        public SendInBlueModel()
        {
            AvailableLists = new List<SelectListItem>();
            AvailableSenders = new List<SelectListItem>();
            AvailableMessageTemplates = new List<SelectListItem>();
        }

        public int ActiveStoreScopeConfiguration { get; set; }

        [NopResourceDisplayName("Plugins.Misc.SendInBlue.Fields.ApiKey")]
        public string ApiKey { get; set; }

        [NopResourceDisplayName("Plugins.Misc.SendInBlue.Fields.MaKey")]
        public string MAKey { get; set; }

        [NopResourceDisplayName("Plugins.Misc.SendInBlue.AccountInfo")]
        public string AccountInfo { get; set; }

        [NopResourceDisplayName("Plugins.Misc.SendInBlue.Fields.List")]
        public int ListId { get; set; }
        public bool ListId_OverrideForStore { get; set; }

        public List<SelectListItem> AvailableLists { get; set; }

        [NopResourceDisplayName("Plugins.Misc.SendInBlue.Fields.NewListName")]
        public string NewListName { get; set; }

        [NopResourceDisplayName("Plugins.Misc.SendInBlue.Fields.AutoSync")]
        public bool AutoSync { get; set; }

        [NopResourceDisplayName("Plugins.Misc.SendInBlue.Fields.AutoSyncEachMinutes")]
        public int AutoSyncEachMinutes { get; set; }

        [NopResourceDisplayName("Plugins.Misc.SendInBlue.Fields.UseSendInBlueSMTP")]
        public bool UseSendInBlueSMTP { get; set; }
        public bool UseSendInBlueSMTP_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Misc.SendInBlue.Fields.SMTPSender")]
        public string SMTPSenderId { get; set; }
        public bool SMTPSenderId_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Misc.SendInBlue.Fields.SMTPPassword")]
        public string SMTPPassword { get; set; }

        public List<SelectListItem> AvailableSenders { get; set; }

        [NopResourceDisplayName("Plugins.Misc.SendInBlue.Fields.UseSMS")]
        public bool UseSMS { get; set; }
        public bool UseSMS_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Misc.SendInBlue.Fields.UseMA")]
        public bool UseMA { get; set; }
        public bool UseMA_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Misc.SendInBlue.Fields.SMSFrom")]
        public string SMSFrom { get; set; }
        public bool SMSFrom_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Misc.SendInBlue.Fields.MyPhoneNumber")]
        public string MyPhoneNumber { get; set; }

        public List<SelectListItem> AvailableMessageTemplates { get; set; }

        [NopResourceDisplayName("Admin.ContentManagement.MessageTemplates.Fields.AllowedTokens")]
        public string AllowedTokens { get; set; }
    }
}