using System.Collections.Generic;
using Nop.Core;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Tasks;
using Nop.Core.Plugins;
using Nop.Plugin.Misc.SendInBlue.Services;
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
        private readonly IScheduleTaskService _scheduleTaskService;
        private readonly ISettingService _settingService;
        private readonly IStoreService _storeService;
        private readonly SendInBlueEmailManager _sendInBlueEmailManager;
        private readonly SendInBlueMarketingAutomationManager _sendInBlueMarketingAutomationManager;
        private readonly IWebHelper _webHelper;
        private readonly ILocalizationService _localizationService;

        #endregion

        #region Ctor

        public SendInBluePlugin(IEmailAccountService emailAccountService,
            IScheduleTaskService scheduleTaskService,
            ISettingService settingService,
            IStoreService storeService,
            SendInBlueEmailManager sendInBlueEmailManager,
            SendInBlueMarketingAutomationManager sendInBlueMarketingAutomationManager,
            IWebHelper webHelper,
            ILocalizationService localizationService)
        {
            this._emailAccountService = emailAccountService;
            this._scheduleTaskService = scheduleTaskService;
            this._settingService = settingService;
            this._storeService = storeService;
            this._sendInBlueEmailManager = sendInBlueEmailManager;
            this._sendInBlueMarketingAutomationManager = sendInBlueMarketingAutomationManager;
            this._webHelper = webHelper;
            this._localizationService = localizationService;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Import subscriptions from nopCommerce to SendInBlue
        /// </summary>
        public void Synchronize()
        {
            _sendInBlueEmailManager.Synchronize();
        }

        /// <summary>
        /// Subscribe user
        /// </summary>
        /// <param name="email">Email of unsubscribed</param>
        public void Subscribe(string email)
        {
            _sendInBlueEmailManager.Subscribe(email);
        }

        /// <summary>
        /// Unsubscribe user
        /// </summary>
        /// <param name="email">Email of unsubscribed</param>
        public void Unsubscribe(string email)
        {
            _sendInBlueEmailManager.Unsubscribe(email);
        }


        public void CartCreated(ShoppingCartItem cartItem)
        {
            _sendInBlueMarketingAutomationManager.CartCreated(cartItem);
        }

        public void CartUpdated(ShoppingCartItem cartItem)
        {
            _sendInBlueMarketingAutomationManager.CartUpdated(cartItem);
        }

        public void CartDeleted(ShoppingCartItem cartItem)
        {
           _sendInBlueMarketingAutomationManager.CartDeleted(cartItem);
        }

        public void OrderCompleted(Order order)
        {
            _sendInBlueEmailManager.UpdateContact(order);
            _sendInBlueMarketingAutomationManager.OrderCompleted(order);
        }

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
            _settingService.SaveSetting(new SendInBlueSettings
            {
                SMSMessageTemplatesIds = new List<int>()
            });

            //install synchronization task
            if (_scheduleTaskService.GetTaskByType("Nop.Plugin.Misc.SendInBlue.Services.SendInBlueSynchronizationTask, Nop.Plugin.Misc.SendInBlue") == null)
            {
                _scheduleTaskService.InsertTask(new ScheduleTask
                {
                    Name = "SendInBlue synchronization",
                    Seconds = 10800,
                    Type = "Nop.Plugin.Misc.SendInBlue.Services.SendInBlueSynchronizationTask, Nop.Plugin.Misc.SendInBlue"
                });
            }

            //locales
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.AccountInfo", "Account information");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.ActivateSMTP", "On your SendinBlue account, the SMTP has not been enabled yet. To request its activation, simply send an email to our support team at contact@sendinblue.com and mention that you will be using the SMTP with the nopCommerce plugin.");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.AddNewSMSNotification", "Add new SMS notification");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.AutoSyncRestart", "If synchronization task parameters has been changed, please restart the application");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.BillingAddressPhone", "Billing address phone number");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.CustomerPhone", "Customer phone number");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.EditTemplate", "Edit template");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.General", "General");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.ImportProcess", "Your import is in process");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.ManualSync", "Manual synchronization");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.MarketingAutomation", "Marketing Automation");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.MyPhone", "Your phone number");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.PhoneType", "Type of phone number");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.SendInBlueTemplate", "SendInBlue email template");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.SMS", "SMS");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.SMSActive", "Is SMS active");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.SMSText", "Text");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.StandardTemplate", "NopCommerce message template");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.Synchronization", "Synchronization");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.TemplateType", "Template type");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.Transactional", "Transactional emails");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.ApiKey", "API key");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.ApiKey.Hint", "Input your SendInBlue account API key.");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.AutoSync", "Auto synchronization");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.AutoSync.Hint", "Use auto synchronization task.");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.AutoSyncEachMinutes", "Period (minutes)");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.AutoSyncEachMinutes.Hint", "Input auto synchronization task period (minutes).");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.List", "List");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.List.Hint", "Choose list of users for the synchronization.");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.MaKey", "Tracker ID");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.MaKey.Hint", "Input your Tracker ID.");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.MyPhoneNumber", "Your phone number");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.MyPhoneNumber.Hint", "Input your phone number for SMS notifications.");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.NewListName", "New list name");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.NewListName.Hint", "Input name for new list of users.");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.SMSFrom", "Send SMS from");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.SMSFrom.Hint", "Input the name of the sender. The number of characters is limited to 11 (alphanumeric format).");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.SMTPSender", "Send emails from");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.SMTPSender.Hint", "Choose sender of your transactional emails.");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.SMTPPassword", "SMTP password");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.SMTPPassword.Hint", "SMTP password.");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.UseSendInBlueSMTP", "Use SendInBlue for notifications");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.UseSendInBlueSMTP.Hint", "Check for using SendInBlue SMTP for sending transactional emails.");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.UseMA", "Use Marketing Automation");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.UseMA.Hint", "Check for enable SendinBlue Automation.");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.UseSMS", "Use SMS notifications");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.UseSMS.Hint", "Check for sending transactional SMS.");
            
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
                var emailAccountId = _settingService.GetSettingByKey<int>("SendInBlueSettings.SendInBlueEmailAccountId",
                    storeId: store.Id, loadSharedValueIfNotFound: true);
                var emailAccount = _emailAccountService.GetEmailAccountById(emailAccountId);
                if (emailAccount != null)
                    _emailAccountService.DeleteEmailAccount(emailAccount);
            }
            
            //settings
            _settingService.DeleteSetting<SendInBlueSettings>();

            //remove scheduled task
            var task = _scheduleTaskService.GetTaskByType("Nop.Plugin.Misc.SendInBlue.Services.SendInBlueSynchronizationTask, Nop.Plugin.Misc.SendInBlue");
            if (task != null)
                _scheduleTaskService.DeleteTask(task);

            //locales
            _localizationService.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.AccountInfo");
            _localizationService.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.ActivateSMTP");
            _localizationService.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.AddNewSMSNotification");
            _localizationService.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.AutoSyncRestart");
            _localizationService.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.BillingAddressPhone");
            _localizationService.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.CustomerPhone");
            _localizationService.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.EditTemplate");
            _localizationService.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.General");
            _localizationService.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.ImportProcess");
            _localizationService.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.ManualSync");
            _localizationService.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.MarketingAutomation");
            _localizationService.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.MyPhone");
            _localizationService.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.PhoneType");
            _localizationService.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.SendInBlueTemplate");
            _localizationService.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.SMS");
            _localizationService.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.SMSActive");
            _localizationService.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.SMSText");
            _localizationService.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.StandardTemplate");
            _localizationService.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.Synchronization");
            _localizationService.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.TemplateType");
            _localizationService.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.Transactional");
            _localizationService.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.ApiKey");
            _localizationService.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.ApiKey.Hint");
            _localizationService.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.AutoSync");
            _localizationService.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.AutoSync.Hint");
            _localizationService.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.AutoSyncEachMinutes");
            _localizationService.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.AutoSyncEachMinutes.Hint");
            _localizationService.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.List");
            _localizationService.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.List.Hint");
            _localizationService.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.MaKey");
            _localizationService.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.MaKey.Hint");
            _localizationService.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.MyPhoneNumber");
            _localizationService.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.MyPhoneNumber.Hint");
            _localizationService.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.NewListName");
            _localizationService.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.NewListName.Hint");
            _localizationService.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.SMSFrom");
            _localizationService.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.SMSFrom.Hint");
            _localizationService.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.SMTPSender");
            _localizationService.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.SMTPSender.Hint");
            _localizationService.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.SMTPPassword");
            _localizationService.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.SMTPPassword.Hint");
            _localizationService.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.UseSendInBlueSMTP");
            _localizationService.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.UseSendInBlueSMTP.Hint");
            _localizationService.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.UseMA");
            _localizationService.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.UseMA.Hint");
            _localizationService.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.UseSMS");
            _localizationService.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.UseSMS.Hint");
            
            base.Uninstall();
        }

        #endregion
    }
}
