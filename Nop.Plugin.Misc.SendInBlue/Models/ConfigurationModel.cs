using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.Misc.SendInBlue.Models
{
    /// <summary>
    /// Represents a configuration model
    /// </summary>
    public class ConfigurationModel
    {
        #region Ctor

        public ConfigurationModel()
        {
            AvailableLists = new List<SelectListItem>();
            AvailableSenders = new List<SelectListItem>();
            AvailableMessageTemplates = new List<SelectListItem>();
        }

        #endregion

        #region Properties

        public int ActiveStoreScopeConfiguration { get; set; }

        [NopResourceDisplayName("Plugins.Misc.SendInBlue.Fields.ApiKey")]
        public string ApiKey { get; set; }

        [NopResourceDisplayName("Plugins.Misc.SendInBlue.Fields.List")]
        public int ListId { get; set; }
        public bool ListId_OverrideForStore { get; set; }
        public List<SelectListItem> AvailableLists { get; set; }

        [NopResourceDisplayName("Plugins.Misc.SendInBlue.Fields.SmtpKey")]
        public string SmtpKey { get; set; }

        [NopResourceDisplayName("Plugins.Misc.SendInBlue.Fields.UseSmtp")]
        public bool UseSmtp { get; set; }
        public bool UseSmtp_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Misc.SendInBlue.Fields.Sender")]
        public string SenderId { get; set; }
        public bool SenderId_OverrideForStore { get; set; }
        public List<SelectListItem> AvailableSenders { get; set; }

        [NopResourceDisplayName("Plugins.Misc.SendInBlue.Fields.UseSmsNotifications")]
        public bool UseSmsNotifications { get; set; }
        public bool UseSmsNotifications_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Misc.SendInBlue.Fields.SmsSenderName")]
        public string SmsSenderName { get; set; }
        public bool SmsSenderName_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Misc.SendInBlue.Fields.StoreOwnerPhoneNumber")]
        public string StoreOwnerPhoneNumber { get; set; }

        [NopResourceDisplayName("Plugins.Misc.SendInBlue.Fields.CampaignList")]
        public int CampaignListId { get; set; }

        [NopResourceDisplayName("Plugins.Misc.SendInBlue.Fields.CampaignSenderName")]
        public string CampaignSenderName { get; set; }

        [NopResourceDisplayName("Plugins.Misc.SendInBlue.Fields.CampaignText")]
        public string CampaignText { get; set; }

        [NopResourceDisplayName("Plugins.Misc.SendInBlue.Fields.MaKey")]
        public string MarketingAutomationKey { get; set; }

        [NopResourceDisplayName("Plugins.Misc.SendInBlue.Fields.UseMarketingAutomation")]
        public bool UseMarketingAutomation { get; set; }
        public bool UseMarketingAutomation_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Misc.SendInBlue.AccountInfo")]
        public string AccountInfo { get; set; }

        [NopResourceDisplayName("Plugins.Misc.SendInBlue.Fields.AllowedTokens")]
        public string AllowedTokens { get; set; }

        public List<SelectListItem> AvailableMessageTemplates { get; set; }

        public bool MarketingAutomationDisabled { get; set; }

        #endregion
    }
}