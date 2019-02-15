using Nop.Core;
using Nop.Core.Domain.Tasks;
using Nop.Core.Plugins;
using Nop.Services.Common;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Messages;
using Nop.Services.Stores;
using Nop.Services.Tasks;

namespace Nop.Plugin.Misc.SendInBlue
{
    /// <summary>
    /// Represents the SendInBlue plugin
    /// </summary>
    public class SendInBluePlugin : BasePlugin, IMiscPlugin
    {
        #region Fields

        private readonly IEmailAccountService _emailAccountService;
        private readonly ILocalizationService _localizationService;
        private readonly IScheduleTaskService _scheduleTaskService;
        private readonly ISettingService _settingService;
        private readonly IStoreService _storeService;
        private readonly IWebHelper _webHelper;

        #endregion

        #region Ctor

        public SendInBluePlugin(IEmailAccountService emailAccountService,
            ILocalizationService localizationService,
            IScheduleTaskService scheduleTaskService,
            ISettingService settingService,
            IStoreService storeService,
            IWebHelper webHelper)
        {
            this._emailAccountService = emailAccountService;
            this._localizationService = localizationService;
            this._scheduleTaskService = scheduleTaskService;
            this._settingService = settingService;
            this._storeService = storeService;
            this._webHelper = webHelper;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Gets a configuration page URL
        /// </summary>
        public override string GetConfigurationPageUrl()
        {
            return $"{_webHelper.GetStoreLocation()}Admin/SendInBlue/Configure";
        }

        /// <summary>
        /// Install the plugin
        /// </summary>
        public override void Install()
        {
            //settings
            _settingService.SaveSetting(new SendInBlueSettings());

            //install synchronization task
            if (_scheduleTaskService.GetTaskByType(SendInBlueDefaults.SynchronizationTask) == null)
            {
                _scheduleTaskService.InsertTask(new ScheduleTask
                {
                    Enabled = true,
                    Seconds = SendInBlueDefaults.DefaultSynchronizationPeriod * 60 * 60,
                    Name = SendInBlueDefaults.SynchronizationTaskName,
                    Type = SendInBlueDefaults.SynchronizationTask,
                });
            }

            //locales
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.AccountInfo", "Account info");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.AccountInfo.Hint", "Display account information.");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.ActivateSMTP", "On your SendinBlue account, the SMTP has not been enabled yet. To request its activation, simply send an email to our support team at contact@sendinblue.com and mention that you will be using the SMTP with the nopCommerce plugin.");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.AddNewSMSNotification", "Add new SMS notification");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.BillingAddressPhone", "Billing address phone number");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.CustomerPhone", "Customer phone number");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.EditTemplate", "Edit template");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.AllowedTokens", "Allowed message tokens");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.AllowedTokens.Hint", "This is a list of the message tokens you can use in your SMS.");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.ApiKey", "API key");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.ApiKey.Hint", "Input your SendInBlue account API key.");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.CampaignList", "List");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.CampaignList.Hint", "Choose list of contacts to send SMS campaign.");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.CampaignSenderName", "Send SMS campaign from");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.CampaignSenderName.Hint", "Input the name of the sender. The number of characters is limited to 11 (alphanumeric format).");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.CampaignText", "Text");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.CampaignText.Hint", "Specify SMS campaign content. The number of characters is limited to 160 for one message.");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.List", "List");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.List.Hint", "Choose list of contacts to synchronize.");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.MaKey", "Tracker ID");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.MaKey.Hint", "Input your Tracker ID.");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.Sender", "Send emails from");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.Sender.Hint", "Choose sender of your transactional emails.");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.SmsSenderName", "Send SMS from");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.SmsSenderName.Hint", "Input the name of the sender. The number of characters is limited to 11 (alphanumeric format).");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.SmtpKey", "SMTP key");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.SmtpKey.Hint", "Specify SMTP key (password).");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.StoreOwnerPhoneNumber", "Store owner phone number");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.StoreOwnerPhoneNumber.Hint", "Input store owner phone number for SMS notifications.");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.UseMarketingAutomation", "Use Marketing Automation");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.UseMarketingAutomation.Hint", "Check for enable SendinBlue Automation.");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.UseSmsNotifications", "Use SMS notifications");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.UseSmsNotifications.Hint", "Check for sending transactional SMS.");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.UseSmtp", "Use SendInBlue SMTP");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.UseSmtp.Hint", "Check for using SendInBlue SMTP for sending transactional emails.");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.General", "General");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.ImportProcess", "Your import is in process");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.ManualSync", "Manual synchronization");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.MarketingAutomation", "Marketing Automation");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.MyPhone", "Store owner phone number");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.PhoneType", "Type of phone number");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.SendInBlueTemplate", "SendInBlue email template");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.SMS", "SMS");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.SMS.Campaigns", "SMS campaigns");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.SMS.Campaigns.Sent", "Campaign successfully sent");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.SMS.Campaigns.Submit", "Send campaign");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.SMSText", "Text");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.StandardTemplate", "Standard message template");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.Synchronization", "Synchronization");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.TemplateType", "Template type");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.Transactional", "Transactional emails");

            base.Install();
        }

        /// <summary>
        /// Uninstall the plugin
        /// </summary>
        public override void Uninstall()
        {
            //smtp accounts
            foreach (var store in _storeService.GetAllStores())
            {
                var emailAccountId = _settingService.GetSettingByKey<int>("SendInBlueSettings.EmailAccountId",
                    storeId: store.Id, loadSharedValueIfNotFound: true);
                var emailAccount = _emailAccountService.GetEmailAccountById(emailAccountId);
                if (emailAccount != null)
                    _emailAccountService.DeleteEmailAccount(emailAccount);
            }

            //settings
            _settingService.DeleteSetting<SendInBlueSettings>();

            //schedule task
            var task = _scheduleTaskService.GetTaskByType(SendInBlueDefaults.SynchronizationTask);
            if (task != null)
                _scheduleTaskService.DeleteTask(task);

            //locales
            _localizationService.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.AccountInfo");
            _localizationService.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.AccountInfo.Hint");
            _localizationService.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.ActivateSMTP");
            _localizationService.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.AddNewSMSNotification");
            _localizationService.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.BillingAddressPhone");
            _localizationService.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.BillingAddressPhone");
            _localizationService.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.CustomerPhone");
            _localizationService.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.CustomerPhone");
            _localizationService.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.EditTemplate");
            _localizationService.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.EditTemplate");
            _localizationService.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.AllowedTokens");
            _localizationService.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.AllowedTokens.Hint");
            _localizationService.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.ApiKey");
            _localizationService.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.ApiKey.Hint");
            _localizationService.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.CampaignList");
            _localizationService.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.CampaignList.Hint");
            _localizationService.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.CampaignSenderName");
            _localizationService.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.CampaignSenderName.Hint");
            _localizationService.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.CampaignText");
            _localizationService.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.CampaignText.Hint");
            _localizationService.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.List");
            _localizationService.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.List.Hint");
            _localizationService.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.MaKey");
            _localizationService.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.MaKey.Hint");
            _localizationService.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.Sender");
            _localizationService.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.Sender.Hint");
            _localizationService.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.SmsSenderName");
            _localizationService.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.SmsSenderName.Hint");
            _localizationService.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.SmtpKey");
            _localizationService.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.SmtpKey.Hint");
            _localizationService.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.StoreOwnerPhoneNumber");
            _localizationService.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.StoreOwnerPhoneNumber.Hint");
            _localizationService.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.UseMarketingAutomation");
            _localizationService.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.UseMarketingAutomation.Hint");
            _localizationService.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.UseSmsNotifications");
            _localizationService.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.UseSmsNotifications.Hint");
            _localizationService.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.UseSmtp");
            _localizationService.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.UseSmtp.Hint");
            _localizationService.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.General");
            _localizationService.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.ImportProcess");
            _localizationService.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.ManualSync");
            _localizationService.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.MarketingAutomation");
            _localizationService.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.MyPhone");
            _localizationService.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.MyPhone");
            _localizationService.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.PhoneType");
            _localizationService.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.SendInBlueTemplate");
            _localizationService.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.SendInBlueTemplate");
            _localizationService.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.SendInBlueTemplate");
            _localizationService.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.SMS");
            _localizationService.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.SMS.Campaigns");
            _localizationService.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.SMS.Campaigns.Sent");
            _localizationService.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.SMS.Campaigns.Submit");
            _localizationService.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.SMSText");
            _localizationService.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.StandardTemplate");
            _localizationService.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.StandardTemplate");
            _localizationService.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.StandardTemplate");
            _localizationService.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.Synchronization");
            _localizationService.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.TemplateType");
            _localizationService.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.Transactional");

            base.Uninstall();
        }

        #endregion
    }
}