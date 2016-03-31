using System.Collections.Generic;
using System.Web.Routing;
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
        private readonly IEmailAccountService _emailAccountService;
        private readonly IScheduleTaskService _scheduleTaskService;
        private readonly ISettingService _settingService;
        protected readonly IStoreService _storeService;
        private readonly SendInBlueEmailManager _sendInBlueEmailManager;
        private readonly SendInBlueSettings _sendInBlueSettings;

        public SendInBluePlugin(IEmailAccountService emailAccountService,
            IScheduleTaskService scheduleTaskService,
            ISettingService settingService,
            IStoreService storeService,
            SendInBlueEmailManager sendInBlueEmailManager,
            SendInBlueSettings sendInBlueSettings)
        {
            this._emailAccountService = emailAccountService;
            this._scheduleTaskService = scheduleTaskService;
            this._settingService = settingService;
            this._storeService = storeService;
            this._sendInBlueEmailManager = sendInBlueEmailManager;
            this._sendInBlueSettings = sendInBlueSettings;
        }

        /// <summary>
        /// Gets a route for plugin configuration
        /// </summary>
        /// <param name="actionName">Action name</param>
        /// <param name="controllerName">Controller name</param>
        /// <param name="routeValues">Route values</param>
        public void GetConfigurationRoute(out string actionName, out string controllerName, out RouteValueDictionary routeValues)
        {
            actionName = "Configure";
            controllerName = "SendInBlue";
            routeValues = new RouteValueDictionary { { "Namespaces", "Nop.Plugin.Misc.SendInBlue.Controllers" }, { "area", null } };
        }

        /// <summary>
        /// Install plugin
        /// </summary>
        public override void Install()
        {
            //settings
            var settings = new SendInBlueSettings { SMSMessageTemplatesIds = new List<int>() };
            _settingService.SaveSetting(settings);

            //install synchronization task
            if (_scheduleTaskService.GetTaskByType("Nop.Plugin.Misc.SendInBlue.Services.SendInBlueSynchronizationTask, Nop.Plugin.Misc.SendInBlue") == null)
            {
                _scheduleTaskService.InsertTask(new ScheduleTask
                {
                    Name = "SendInBlue synchronization",
                    Seconds = 3600,
                    Type = "Nop.Plugin.Misc.SendInBlue.Services.SendInBlueSynchronizationTask, Nop.Plugin.Misc.SendInBlue",
                });
            }

            //locales
            this.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.AccountInfo", "Account information");
            this.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.ActivateSMTP", "If status is disabled, you need activate your SendInBlue SMTP account as described at https://resources.sendinblue.com/en/activate_smtp_account/");
            this.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.AddNewSMSNotification", "Add new SMS notification");
            this.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.AutoSyncRestart", "If synchronization task parameters has been changed, please restart the application");
            this.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.BillingAddressPhone", "Billing address phone number");
            this.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.CustomerPhone", "Customer phone number");
            this.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.EditTemplate", "Edit template");
            this.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.General", "General");
            this.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.ImportProcess", "Your import is in process");
            this.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.ManualSync", "Manual synchronization");
            this.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.MyPhone", "Your phone number");
            this.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.PhoneType", "Type of phone number");
            this.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.SendInBlueTemplate", "SendInBlue email template");
            this.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.SMS", "SMS");
            this.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.SMSActive", "Is SMS active");
            this.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.SMSText", "Text");
            this.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.StandartTemplate", "NopCommerce message template");
            this.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.Statistics", "Statistics");
            this.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.Synchronization", "Synchronization");
            this.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.TemplateType", "Template type");
            this.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.Transactional", "Transactional emails");
            this.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.ApiKey", "API key");
            this.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.ApiKey.Hint", "Input your SendInBlue account API key.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.AutoSync", "Auto synchronization");
            this.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.AutoSync.Hint", "Use auto synchronization task.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.AutoSyncEachMinutes", "Period (minutes)");
            this.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.AutoSyncEachMinutes.Hint", "Input auto synchronization task period (minutes).");
            this.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.List", "List");
            this.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.List.Hint", "Choose list of users for the synchronization.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.MyPhoneNumber", "Your phone number");
            this.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.MyPhoneNumber.Hint", "Input your phone number for SMS notifications.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.NewListName", "New list name");
            this.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.NewListName.Hint", "Input name for new list of users.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.SMSFrom", "Send SMS from");
            this.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.SMSFrom.Hint", "Input the name of the sender. The number of characters is limited to 11 (alphanumeric format).");
            this.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.SMTPSender", "Send emails from");
            this.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.SMTPSender.Hint", "Choose sender of your transactional emails.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.UseSendInBlueSMTP", "Use SendInBlue for notifications");
            this.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.UseSendInBlueSMTP.Hint", "Check for using SendInBlue SMTP for sending transactional emails.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.UseSMS", "Use SMS notifications");
            this.AddOrUpdatePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.UseSMS.Hint", "Check for sending transactional SMS.");
            
            base.Install();
        }

        /// <summary>
        /// Uninstall plugin
        /// </summary>
        public override void Uninstall()
        {
            //smtp accounts
            foreach (var store in _storeService.GetAllStores())
            {
                var settings = _settingService.LoadSetting<SendInBlueSettings>(store.Id);
                var emailAccount = _emailAccountService.GetEmailAccountById(settings.SendInBlueEmailAccountId);
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
            this.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.AccountInfo");
            this.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.ActivateSMTP");
            this.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.AddNewSMSNotification");
            this.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.AutoSyncRestart");
            this.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.BillingAddressPhone");
            this.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.CustomerPhone");
            this.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.EditTemplate");
            this.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.General");
            this.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.ImportProcess");
            this.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.ManualSync");
            this.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.MyPhone");
            this.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.PhoneType");
            this.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.SendInBlueTemplate");
            this.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.SMS");
            this.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.SMSActive");
            this.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.SMSText");
            this.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.StandartTemplate");
            this.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.Statistics");
            this.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.Synchronization");
            this.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.TemplateType");
            this.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.Transactional");
            this.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.ApiKey");
            this.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.ApiKey.Hint");
            this.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.AutoSync");
            this.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.AutoSync.Hint");
            this.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.AutoSyncEachMinutes");
            this.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.AutoSyncEachMinutes.Hint");
            this.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.List");
            this.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.List.Hint");
            this.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.MyPhoneNumber");
            this.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.MyPhoneNumber.Hint");
            this.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.NewListName");
            this.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.NewListName.Hint");
            this.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.SMSFrom");
            this.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.SMSFrom.Hint");
            this.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.SMTPSender");
            this.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.SMTPSender.Hint");
            this.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.UseSendInBlueSMTP");
            this.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.UseSendInBlueSMTP.Hint");
            this.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.UseSMS");
            this.DeletePluginLocaleResource("Plugins.Misc.SendInBlue.Fields.UseSMS.Hint");
            
            base.Uninstall();
        }

        /// <summary>
        /// Synchronize SendInBlue and nopCommerce users
        /// </summary>
        public void Synchronize()
        {
            _sendInBlueEmailManager.Synchronize();
        }

        /// <summary>
        /// Unsubscribe user
        /// </summary>
        /// <param name="email">Email of unsubscribed</param>
        public void Unsubscribe(string email)
        {
            _sendInBlueEmailManager.Unsubscribe(email);
        }
    }
}
