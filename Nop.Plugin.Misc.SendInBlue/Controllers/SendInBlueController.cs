using System;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Core;
using Nop.Core.Domain.Messages;
using Nop.Plugin.Misc.SendInBlue.Models;
using Nop.Plugin.Misc.SendInBlue.Services;
using Nop.Services.Common;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Services.Messages;
using Nop.Services.Stores;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Kendoui;
using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc;
using Nop.Web.Framework.Mvc.Filters;
using Nop.Web.Framework.UI;

namespace Nop.Plugin.Misc.SendInBlue.Controllers
{
    public class SendInBlueController : BasePluginController
    {
        #region Fields

        private readonly IEmailAccountService _emailAccountService;
        private readonly IGenericAttributeService _genericAttributeService;
        private readonly ILocalizationService _localizationService;
        private readonly ILogger _logger;
        private readonly IMessageTemplateService _messageTemplateService;
        private readonly IMessageTokenProvider _messageTokenProvider;
        private readonly ISettingService _settingService;
        private readonly IStoreContext _storeContext;
        private readonly IStoreMappingService _storeMappingService;
        private readonly IStoreService _storeService;
        private readonly MessageTemplatesSettings _messageTemplatesSettings;
        private readonly SendInBlueManager _sendInBlueEmailManager;

        #endregion

        #region Ctor

        public SendInBlueController(IEmailAccountService emailAccountService,
            IGenericAttributeService genericAttributeService,
            ILocalizationService localizationService,
            ILogger logger,
            IMessageTemplateService messageTemplateService,
            IMessageTokenProvider messageTokenProvider,
            ISettingService settingService,
            IStoreContext storeContext,
            IStoreMappingService storeMappingService,
            IStoreService storeService,
            MessageTemplatesSettings messageTemplatesSettings,
            SendInBlueManager sendInBlueEmailManager)
        {
            this._emailAccountService = emailAccountService;
            this._genericAttributeService = genericAttributeService;
            this._localizationService = localizationService;
            this._logger = logger;
            this._messageTemplateService = messageTemplateService;
            this._messageTokenProvider = messageTokenProvider;
            this._settingService = settingService;
            this._storeContext = storeContext;
            this._storeMappingService = storeMappingService;
            this._storeService = storeService;
            this._messageTemplatesSettings = messageTemplatesSettings;
            this._sendInBlueEmailManager = sendInBlueEmailManager;
        }

        #endregion

        #region Utilities

        /// <summary>
        /// Prepare SendInBlueModel
        /// </summary>
        /// <param name="model">Model</param>
        protected void PrepareModel(ConfigurationModel model)
        {
            //load settings for active store scope
            var storeId = _storeContext.ActiveStoreScopeConfiguration;
            var sendInBlueSettings = _settingService.LoadSetting<SendInBlueSettings>(storeId);

            //whether plugin is configured
            if (string.IsNullOrEmpty(sendInBlueSettings.ApiKey))
                return;

            //prepare common properties
            model.ActiveStoreScopeConfiguration = storeId;
            model.ApiKey = sendInBlueSettings.ApiKey;
            model.ListId = sendInBlueSettings.ListId;
            model.SmtpKey = sendInBlueSettings.SmtpKey;
            model.SenderId = sendInBlueSettings.SenderId;
            model.UseSmsNotifications = sendInBlueSettings.UseSmsNotifications;
            model.SmsSenderName = sendInBlueSettings.SmsSenderName;
            model.StoreOwnerPhoneNumber = sendInBlueSettings.StoreOwnerPhoneNumber;
            model.MarketingAutomationKey = sendInBlueSettings.MarketingAutomationKey;
            model.UseMarketingAutomation = sendInBlueSettings.UseMarketingAutomation;

            //prepare overridable settings
            if (storeId > 0)
            {
                model.ListId_OverrideForStore = _settingService.SettingExists(sendInBlueSettings, x => x.ListId, storeId);
                model.UseSmtp_OverrideForStore = _settingService.SettingExists(sendInBlueSettings, x => x.UseSmtp, storeId);
                model.SenderId_OverrideForStore = _settingService.SettingExists(sendInBlueSettings, x => x.SenderId, storeId);
                model.UseSmsNotifications_OverrideForStore = _settingService.SettingExists(sendInBlueSettings, x => x.UseSmsNotifications, storeId);
                model.SmsSenderName_OverrideForStore = _settingService.SettingExists(sendInBlueSettings, x => x.SmsSenderName, storeId);
                model.UseMarketingAutomation_OverrideForStore = _settingService.SettingExists(sendInBlueSettings, x => x.UseMarketingAutomation, storeId);
            }

            //check whether email account exists
            if (sendInBlueSettings.UseSmtp && _emailAccountService.GetEmailAccountById(sendInBlueSettings.EmailAccountId) != null)
                model.UseSmtp = sendInBlueSettings.UseSmtp;

            //get account info
            var (accountInfo, marketingAutomationEnabled, accountErrors) = _sendInBlueEmailManager.GetAccountInfo();
            model.AccountInfo = accountInfo;
            model.MarketingAutomationDisabled = !marketingAutomationEnabled;
            if (!string.IsNullOrEmpty(accountErrors))
                ErrorNotification(accountErrors);

            //check SMTP status
            var (smtpEnabled, smtpErrors) = _sendInBlueEmailManager.SmtpIsEnabled();
            if (!string.IsNullOrEmpty(smtpErrors))
                ErrorNotification(smtpErrors);

            //get available contact lists to synchronize
            var (lists, listsErrors) = _sendInBlueEmailManager.GetLists();
            model.AvailableLists = lists.Select(list => new SelectListItem(list.Name, list.Id)).ToList();
            model.AvailableLists.Insert(0, new SelectListItem("Select list", "0"));
            if (!string.IsNullOrEmpty(listsErrors))
                ErrorNotification(listsErrors);

            //get available senders of emails from account
            var (senders, sendersErrors) = _sendInBlueEmailManager.GetSenders();
            model.AvailableSenders = senders.Select(list => new SelectListItem(list.Name, list.Id)).ToList();
            model.AvailableSenders.Insert(0, new SelectListItem("Select sender", "0"));
            if (!string.IsNullOrEmpty(sendersErrors))
                ErrorNotification(sendersErrors);

            //get message templates
            model.AvailableMessageTemplates = _messageTemplateService.GetAllMessageTemplates(storeId).Select(messageTemplate =>
            {
                var name = messageTemplate.Name;
                if (storeId == 0 && messageTemplate.LimitedToStores)
                {
                    var storeIds = _storeMappingService.GetStoresIdsWithAccess(messageTemplate);
                    var storeNames = _storeService.GetAllStores().Where(store => storeIds.Contains(store.Id)).Select(store => store.Name);
                    name = $"{name} ({string.Join(',', storeNames)})";
                }

                return new SelectListItem(name, messageTemplate.Id.ToString());
            }).ToList();

            //get allowed tokens
            model.AllowedTokens = string.Join(", ", _messageTokenProvider.GetListOfAllowedTokens());

            //create attributes in account
            var attributesErrors = _sendInBlueEmailManager.PrepareAttributes();
            if (!string.IsNullOrEmpty(attributesErrors))
                ErrorNotification(attributesErrors);

            //try to set account partner
            if (!sendInBlueSettings.PartnerValueSet)
            {
                var partnerSet = _sendInBlueEmailManager.SetPartner();
                if (partnerSet)
                {
                    sendInBlueSettings.PartnerValueSet = true;
                    _settingService.SaveSetting(sendInBlueSettings, x => x.PartnerValueSet, clearCache: false);
                    _settingService.ClearCache();
                }
            }
        }

        /// <summary>
        /// Save selected TAB name
        /// </summary>
        /// <param name="tabName">Tab name to save; empty to automatically detect it</param>
        /// <param name="persistForTheNextRequest">A value indicating whether a message should be persisted for the next request</param>
        protected void SaveSelectedTabName(string tabName = "", bool persistForTheNextRequest = true)
        {
            //keep this method synchronized with
            //"GetSelectedTabName" method of \Nop.Web.Framework\HtmlExtensions.cs
            if (string.IsNullOrEmpty(tabName))
            {
                tabName = Request.Form["selected-tab-name"];
            }

            if (string.IsNullOrEmpty(tabName)) return;
            const string dataKey = "nop.selected-tab-name";
            if (persistForTheNextRequest)
            {
                TempData[dataKey] = tabName;
            }
            else
            {
                ViewData[dataKey] = tabName;
            }
        }

        #endregion

        #region Methods

        [AuthorizeAdmin]
        [Area(AreaNames.Admin)]
        public IActionResult Configure()
        {
            var model = new ConfigurationModel();
            PrepareModel(model);

            return View("~/Plugins/Misc.SendInBlue/Views/Configure.cshtml", model);
        }

        [AuthorizeAdmin]
        [Area(AreaNames.Admin)]
        [HttpPost, ActionName("Configure")]
        [FormValueRequired("save")]
        public IActionResult Configure(ConfigurationModel model)
        {
            if (!ModelState.IsValid)
                return Configure();

            var storeId = _storeContext.ActiveStoreScopeConfiguration;
            var sendInBlueSettings = _settingService.LoadSetting<SendInBlueSettings>(storeId);

            //set API key
            sendInBlueSettings.ApiKey = model.ApiKey;
            _settingService.SaveSetting(sendInBlueSettings, x => x.ApiKey, clearCache: false);
            _settingService.ClearCache();

            SuccessNotification(_localizationService.GetResource("Admin.Plugins.Saved"));

            return Configure();
        }

        [AuthorizeAdmin]
        [Area(AreaNames.Admin)]
        [HttpPost, ActionName("Configure")]
        [FormValueRequired("saveSync")]
        public IActionResult SaveSynchronization(ConfigurationModel model)
        {
            if (!ModelState.IsValid)
                return Configure();

            var storeId = _storeContext.ActiveStoreScopeConfiguration;
            var sendInBlueSettings = _settingService.LoadSetting<SendInBlueSettings>(storeId);

            //create webhook for the unsubscribe event
            sendInBlueSettings.UnsubscribeWebhookId = _sendInBlueEmailManager.GetUnsubscribeWebHookId();
            _settingService.SaveSetting(sendInBlueSettings, x => x.UnsubscribeWebhookId, clearCache: false);

            //set list of contacts to synchronize
            sendInBlueSettings.ListId = model.ListId;
            _settingService.SaveSettingOverridablePerStore(sendInBlueSettings, x => x.ListId, model.ListId_OverrideForStore, storeId, false);

            //now clear settings cache
            _settingService.ClearCache();

            SuccessNotification(_localizationService.GetResource("Admin.Plugins.Saved"));

            //select "synchronization" tab
            SaveSelectedTabName("tab-synchronization");

            return Configure();
        }

        [AuthorizeAdmin]
        [Area(AreaNames.Admin)]
        [HttpPost, ActionName("Configure")]
        [FormValueRequired("sync")]
        public IActionResult Synchronization(ConfigurationModel model)
        {
            if (!ModelState.IsValid)
                return Configure();

            //synchronize contacts of selected store
            var messages = _sendInBlueEmailManager.Synchronize(false, _storeContext.ActiveStoreScopeConfiguration);
            foreach (var message in messages)
            {
                AddNotification(message.Type, message.Message, false);
            }
            if (!messages.Any(message => message.Type == NotifyType.Error))
            {
                TempData["synchronizationStart"] = true;
                SuccessNotification(_localizationService.GetResource("Plugins.Misc.SendInBlue.ImportProcess"));
            }

            //select "synchronization" tab
            SaveSelectedTabName("tab-synchronization");

            return Configure();
        }

        [AuthorizeAdmin]
        [Area(AreaNames.Admin)]
        public string GetSynchronizationInfo()
        {
            return TempData["synchronizationEnd"]?.ToString() ?? string.Empty;
        }

        [AuthorizeAdmin]
        [Area(AreaNames.Admin)]
        [HttpPost, ActionName("Configure")]
        [FormValueRequired("saveSMTP")]
        public IActionResult ConfigureSMTP(ConfigurationModel model)
        {
            if (!ModelState.IsValid)
                return Configure();

            var storeId = _storeContext.ActiveStoreScopeConfiguration;
            var sendInBlueSettings = _settingService.LoadSetting<SendInBlueSettings>(storeId);

            if (model.UseSmtp)
            {
                //set case invariant for true because tokens are used in uppercase format in SendInBlue's transactional emails
                _messageTemplatesSettings.CaseInvariantReplacement = true;
                _settingService.SaveSetting(_messageTemplatesSettings, x => x.CaseInvariantReplacement, clearCache: false);

                //check whether SMTP enabled on account
                var (smtpIsEnabled, smtpErrors) = _sendInBlueEmailManager.SmtpIsEnabled();
                if (smtpIsEnabled)
                {
                    //get email account or create new one
                    var (emailAccountId, emailAccountErrors) = _sendInBlueEmailManager.GetEmailAccountId(model.SenderId, model.SmtpKey);
                    sendInBlueSettings.EmailAccountId = emailAccountId;
                    _settingService.SaveSetting(sendInBlueSettings, x => x.EmailAccountId, storeId, false);
                    if (!string.IsNullOrEmpty(emailAccountErrors))
                        ErrorNotification(emailAccountErrors);

                    //synchronize message templates tokens with transactional attributes
                    var tokens = _messageTokenProvider.GetListOfAllowedTokens().ToList();
                    var attributesErrors = _sendInBlueEmailManager.PrepareTransactionalAttributes(tokens);
                    if (!string.IsNullOrEmpty(attributesErrors))
                        ErrorNotification(attributesErrors);
                }
                else
                {
                    //need to activate SMTP account
                    WarningNotification(_localizationService.GetResource("Plugins.Misc.SendInBlue.ActivateSMTP"));
                    model.UseSmtp = false;
                }
                if (!string.IsNullOrEmpty(smtpErrors))
                    ErrorNotification(smtpErrors);
            }

            //set whether to use SMTP 
            sendInBlueSettings.UseSmtp = model.UseSmtp;
            _settingService.SaveSettingOverridablePerStore(sendInBlueSettings, x => x.UseSmtp, model.UseSmtp_OverrideForStore, storeId, false);

            //set sender of transactional emails
            sendInBlueSettings.SenderId = model.SenderId;
            _settingService.SaveSettingOverridablePerStore(sendInBlueSettings, x => x.SenderId, model.SenderId_OverrideForStore, storeId, false);

            //set SMTP key
            sendInBlueSettings.SmtpKey = model.SmtpKey;
            _settingService.SaveSetting(sendInBlueSettings, x => x.SmtpKey, clearCache: false);

            //now clear settings cache
            _settingService.ClearCache();

            SuccessNotification(_localizationService.GetResource("Admin.Plugins.Saved"));

            //select "transactional" tab
            SaveSelectedTabName("tab-transactional");

            return Configure();
        }

        [HttpPost]
        [AuthorizeAdmin]
        [Area(AreaNames.Admin)]
        public IActionResult MessageList(MessageTemplateModel model)
        {
            var storeId = _storeContext.ActiveStoreScopeConfiguration;
            var messageTemplates = _messageTemplateService.GetAllMessageTemplates(storeId);

            var gridModel = new DataSourceResult
            {
                Data = messageTemplates.Select(messageTemplate =>
                {
                    //standard template of message is edited in the admin area, SendInBlue template is edited in the SendInBlue account
                    var isStandardTemplate = !_genericAttributeService.GetAttribute<bool>(messageTemplate, SendInBlueDefaults.SendInBlueTemplateAttribute);
                    var templateId = _genericAttributeService.GetAttribute<int>(messageTemplate, SendInBlueDefaults.TemplateIdAttribute);
                    var stores = _storeService.GetAllStores()
                        .Where(store => !messageTemplate.LimitedToStores || _storeMappingService.GetStoresIdsWithAccess(messageTemplate).Contains(store.Id))
                        .Aggregate(string.Empty, (current, next) => $"{current}, {next.Name}").Trim(',');

                    return new MessageTemplateModel
                    {
                        Id = messageTemplate.Id,
                        Name = messageTemplate.Name,
                        IsActive = messageTemplate.IsActive,
                        ListOfStores = stores,
                        TemplateTypeId = isStandardTemplate ? 0 : 1,
                        TemplateType = isStandardTemplate
                            ? _localizationService.GetResource("Plugins.Misc.SendInBlue.StandardTemplate")
                            : _localizationService.GetResource("Plugins.Misc.SendInBlue.SendInBlueTemplate"),
                        EditLink = isStandardTemplate
                            ? Url.Action("Edit", "MessageTemplate", new { id = messageTemplate.Id, area = AreaNames.Admin })
                            : $"{string.Format(SendInBlueDefaults.EditMessageTemplateUrl, templateId)}"
                    };
                }),
                Total = messageTemplates.Count
            };

            return Json(gridModel);
        }

        [HttpPost]
        [AuthorizeAdmin]
        [Area(AreaNames.Admin)]
        public IActionResult MessageUpdate(MessageTemplateModel model)
        {
            if (!ModelState.IsValid)
                return ErrorForKendoGridJson(ModelState.SerializeErrors().ToString());

            var message = _messageTemplateService.GetMessageTemplateById(model.Id);

            //standard message template
            if (model.TemplateTypeId == 0)
            {
                _genericAttributeService.SaveAttribute(message, SendInBlueDefaults.SendInBlueTemplateAttribute, false);
                model.TemplateType = _localizationService.GetResource("Plugins.Misc.SendInBlue.StandardTemplate");
                model.EditLink = Url.Action("Edit", "MessageTemplate", new { id = model.Id, area = AreaNames.Admin });
            }

            //SendInBlue message template
            if (model.TemplateTypeId == 1)
            {
                var storeId = _storeContext.ActiveStoreScopeConfiguration;
                var sendInBlueSettings = _settingService.LoadSetting<SendInBlueSettings>(storeId);

                //get template or create new one
                var currentTemplateId = _genericAttributeService.GetAttribute<int>(message, SendInBlueDefaults.TemplateIdAttribute);
                var templateId = _sendInBlueEmailManager.GetTemplateId(currentTemplateId, message,
                    _emailAccountService.GetEmailAccountById(sendInBlueSettings.EmailAccountId));

                _genericAttributeService.SaveAttribute(message, SendInBlueDefaults.SendInBlueTemplateAttribute, true);
                _genericAttributeService.SaveAttribute(message, SendInBlueDefaults.TemplateIdAttribute, templateId);
                model.TemplateType = _localizationService.GetResource("Plugins.Misc.SendInBlue.SendInBlueTemplate");
                model.EditLink = $"{string.Format(SendInBlueDefaults.EditMessageTemplateUrl, templateId)}";
            }

            //update message template
            if (model.IsActive == message.IsActive)
                return new NullJsonResult();

            message.IsActive = model.IsActive;
            _messageTemplateService.UpdateMessageTemplate(message);

            return new NullJsonResult();
        }

        [AuthorizeAdmin]
        [Area(AreaNames.Admin)]
        [HttpPost, ActionName("Configure")]
        [FormValueRequired("saveSMS")]
        public IActionResult ConfigureSMS(ConfigurationModel model)
        {
            if (!ModelState.IsValid)
                return Configure();

            var storeId = _storeContext.ActiveStoreScopeConfiguration;
            var sendInBlueSettings = _settingService.LoadSetting<SendInBlueSettings>(storeId);

            sendInBlueSettings.UseSmsNotifications = model.UseSmsNotifications;
            _settingService.SaveSettingOverridablePerStore(sendInBlueSettings, x => x.UseSmsNotifications, model.UseSmsNotifications_OverrideForStore, storeId, false);
            sendInBlueSettings.SmsSenderName = model.SmsSenderName;
            _settingService.SaveSettingOverridablePerStore(sendInBlueSettings, x => x.SmsSenderName, model.SmsSenderName_OverrideForStore, storeId, false);
            sendInBlueSettings.StoreOwnerPhoneNumber = model.StoreOwnerPhoneNumber;
            _settingService.SaveSetting(sendInBlueSettings, x => x.StoreOwnerPhoneNumber, clearCache: false);

            //now clear settings cache
            _settingService.ClearCache();

            SuccessNotification(_localizationService.GetResource("Admin.Plugins.Saved"));

            //select "sms" tab
            SaveSelectedTabName("tab-sms");

            return Configure();
        }

        [AuthorizeAdmin]
        [Area(AreaNames.Admin)]
        [HttpPost, ActionName("Configure")]
        [FormValueRequired("submitCampaign")]
        public IActionResult SubmitCampaign(ConfigurationModel model)
        {
            if (!ModelState.IsValid)
                return Configure();

            var campaignErrors = _sendInBlueEmailManager.SendSMSCampaign(model.CampaignListId, model.CampaignSenderName, model.CampaignText);
            if (!string.IsNullOrEmpty(campaignErrors))
                ErrorNotification(campaignErrors);
            else
                SuccessNotification(_localizationService.GetResource("Plugins.Misc.SendInBlue.SMS.Campaigns.Sent"));

            //select "sms" tab
            SaveSelectedTabName("tab-sms");

            return Configure();
        }

        [AuthorizeAdmin]
        [Area(AreaNames.Admin)]
        [HttpPost, ActionName("Configure")]
        [FormValueRequired("saveMA")]
        public IActionResult ConfigureMA(ConfigurationModel model)
        {
            if (!ModelState.IsValid)
                return Configure();

            var storeId = _storeContext.ActiveStoreScopeConfiguration;
            var sendInBlueSettings = _settingService.LoadSetting<SendInBlueSettings>(storeId);

            sendInBlueSettings.UseMarketingAutomation = model.UseMarketingAutomation;
            _settingService.SaveSettingOverridablePerStore(sendInBlueSettings, x => x.UseMarketingAutomation, model.UseMarketingAutomation_OverrideForStore, storeId, false);
            sendInBlueSettings.MarketingAutomationKey = model.MarketingAutomationKey;
            _settingService.SaveSetting(sendInBlueSettings, x => x.MarketingAutomationKey, clearCache: false);

            //now clear settings cache
            _settingService.ClearCache();

            SuccessNotification(_localizationService.GetResource("Admin.Plugins.Saved"));

            //select "ma" tab
            SaveSelectedTabName("tab-ma");

            return Configure();
        }

        [HttpPost]
        [AuthorizeAdmin]
        [Area(AreaNames.Admin)]
        public IActionResult SMSList(SmsModel model)
        {
            var storeId = _storeContext.ActiveStoreScopeConfiguration;
            var sendInBlueSettings = _settingService.LoadSetting<SendInBlueSettings>(storeId);

            //get message templates which are sending in SMS
            var messageTemplates = _messageTemplateService.GetAllMessageTemplates(storeId)
                .Where(messageTemplate => _genericAttributeService.GetAttribute<bool>(messageTemplate, SendInBlueDefaults.UseSmsAttribute))
                .ToList();

            var gridModel = new DataSourceResult
            {
                Data = messageTemplates.Select(messageTemplate =>
                {
                    var phoneTypeID = _genericAttributeService.GetAttribute<int>(messageTemplate, SendInBlueDefaults.PhoneTypeAttribute);
                    var smsModel = new SmsModel
                    {
                        Id = messageTemplate.Id,
                        MessageId = messageTemplate.Id,
                        Name = messageTemplate.Name,
                        PhoneTypeId = phoneTypeID,
                        Text = _genericAttributeService.GetAttribute<string>(messageTemplate, SendInBlueDefaults.SmsTextAttribute)
                    };

                    if (storeId == 0)
                    {
                        if (storeId == 0 && messageTemplate.LimitedToStores)
                        {
                            var storeIds = _storeMappingService.GetStoresIdsWithAccess(messageTemplate);
                            var storeNames = _storeService.GetAllStores().Where(store => storeIds.Contains(store.Id)).Select(store => store.Name);
                            smsModel.Name = $"{smsModel.Name} ({string.Join(',', storeNames)})";
                        }
                    }

                    //choose phone number to send SMS
                    //currently supported: "my phone" (filled on the configuration page), customer phone, phone of the billing address
                    switch (phoneTypeID)
                    {
                        case 0:
                            smsModel.PhoneType = _localizationService.GetResource("Plugins.Misc.SendInBlue.MyPhone");
                            break;
                        case 1:
                            smsModel.PhoneType = _localizationService.GetResource("Plugins.Misc.SendInBlue.CustomerPhone");
                            break;
                        case 2:
                            smsModel.PhoneType = _localizationService.GetResource("Plugins.Misc.SendInBlue.BillingAddressPhone");
                            break;
                        default:
                            break;
                    }

                    return smsModel;
                }),
                Total = messageTemplates.Count
            };

            return Json(gridModel);
        }

        [HttpPost]
        [AuthorizeAdmin]
        [Area(AreaNames.Admin)]
        public IActionResult SMSAdd(SmsModel model)
        {
            if (!ModelState.IsValid)
                return Json(new DataSourceResult { Errors = ModelState.SerializeErrors() });

            var message = _messageTemplateService.GetMessageTemplateById(model.MessageId);
            if (message != null)
            {
                _genericAttributeService.SaveAttribute(message, SendInBlueDefaults.UseSmsAttribute, true);
                _genericAttributeService.SaveAttribute(message, SendInBlueDefaults.SmsTextAttribute, model.Text);
                _genericAttributeService.SaveAttribute(message, SendInBlueDefaults.PhoneTypeAttribute, model.PhoneTypeId);
            }

            return new NullJsonResult();
        }

        [HttpPost]
        [AuthorizeAdmin]
        [Area(AreaNames.Admin)]
        public IActionResult SMSUpdate(SmsModel model)
        {
            if (!ModelState.IsValid)
                return Json(new DataSourceResult { Errors = ModelState.SerializeErrors() });

            var message = _messageTemplateService.GetMessageTemplateById(model.MessageId);
            if (message != null)
            {
                _genericAttributeService.SaveAttribute(message, SendInBlueDefaults.UseSmsAttribute, true);
                _genericAttributeService.SaveAttribute(message, SendInBlueDefaults.SmsTextAttribute, model.Text);
                _genericAttributeService.SaveAttribute(message, SendInBlueDefaults.PhoneTypeAttribute, model.PhoneTypeId);
            }

            if (model.Id != model.MessageId)
                SMSDelete(new SmsModel { MessageId = model.Id });

            return new NullJsonResult();
        }

        [HttpPost]
        [AuthorizeAdmin]
        [Area(AreaNames.Admin)]
        public IActionResult SMSDelete(SmsModel model)
        {
            if (!ModelState.IsValid)
                return Json(new DataSourceResult { Errors = ModelState.SerializeErrors() });

            //delete generic attributes
            var message = _messageTemplateService.GetMessageTemplateById(model.MessageId);
            if (message != null)
            {
                _genericAttributeService.SaveAttribute<bool?>(message, SendInBlueDefaults.UseSmsAttribute, null);
                _genericAttributeService.SaveAttribute<string>(message, SendInBlueDefaults.SmsTextAttribute, null);
                _genericAttributeService.SaveAttribute<int?>(message, SendInBlueDefaults.PhoneTypeAttribute, null);
            }

            return new NullJsonResult();
        }

        public IActionResult ImportContacts(BaseNopModel model)
        {
            var form = model.Form;
            try
            {
                //logging info
                var logInfo = string.Format("SendInBlue synchronization: New emails {1},{0} Existing emails {2},{0} Invalid emails {3},{0} Duplicates emails {4}{0}",
                    Environment.NewLine, form["new_emails"], form["emails_exists"], form["invalid_email"], form["duplicates_email"]);
                _logger.Information(logInfo);

                //display info on configuration page in case of the manually synchronization
                TempData["synchronizationEnd"] = logInfo;
            }
            catch (Exception ex)
            {
                _logger.Error(ex.Message, ex);
                TempData["synchronizationEnd"] = ex.Message;
            }

            return Ok();
        }

        [HttpPost]
        public IActionResult UnsubscribeWebHook()
        {
            try
            {
                using (var streamReader = new StreamReader(Request.Body))
                {
                    _sendInBlueEmailManager.UnsubscribeWebhook(streamReader.ReadToEnd());
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex.Message, ex);
            }

            return Ok();
        }

        #endregion
    }
}