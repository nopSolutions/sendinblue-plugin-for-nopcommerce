using System;
using System.Linq;
using System.IO;
using System.Net;
using System.Web;
using System.Web.Mvc;
using Nop.Core;
using Nop.Core.Domain.Messages;
using Nop.Core.Domain.Tasks;
using Nop.Plugin.Misc.SendInBlue.Models;
using Nop.Plugin.Misc.SendInBlue.Services;
using Nop.Services.Common;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Services.Messages;
using Nop.Services.Stores;
using Nop.Services.Tasks;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Kendoui;
using Nop.Web.Framework.Mvc;

namespace Nop.Plugin.Misc.SendInBlue.Controllers
{
    [AdminAuthorize]
    public class SendInBlueController : BasePluginController
    {
        /// <summary>
        /// Base URL for the editing of message template on SendInBlue account
        /// </summary>
        private const string EDIT_TEMPLATE_URL = "https://my.sendinblue.com/camp/step1/type/template/id/";

        #region Fields

        private readonly HttpContextBase _httpContext;
        private readonly IEmailAccountService _emailAccountService;
        private readonly IGenericAttributeService _genericAttributeService;
        private readonly ILocalizationService _localizationService;
        private readonly ILogger _logger;
        private readonly IMessageTemplateService _messageTemplateService;
        private readonly IMessageTokenProvider _messageTokenProvider;
        private readonly IScheduleTaskService _scheduleTaskService;
        private readonly ISettingService _settingService;
        private readonly IStoreMappingService _storeMappingService;
        private readonly IStoreService _storeService;
        private readonly IWorkContext _workContext;
        private readonly SendInBlueEmailManager _sendInBlueEmailManager;

        #endregion

        #region Ctor

        public SendInBlueController(HttpContextBase httpContext,
            IEmailAccountService emailAccountService,
            IGenericAttributeService genericAttributeService,
            ILocalizationService localizationService,
            ILogger logger,
            IMessageTemplateService messageTemplateService,
            IMessageTokenProvider messageTokenProvider,
            IScheduleTaskService scheduleTaskService,
            ISettingService settingService,
            IStoreMappingService storeMappingService,
            IStoreService storeService,
            IWorkContext workContext,
            SendInBlueEmailManager sendInBlueEmailManager)
        {
            this._httpContext = httpContext;
            this._emailAccountService = emailAccountService;
            this._genericAttributeService = genericAttributeService;
            this._localizationService = localizationService;
            this._logger = logger;
            this._messageTemplateService = messageTemplateService;
            this._messageTokenProvider = messageTokenProvider;
            this._scheduleTaskService = scheduleTaskService;
            this._settingService = settingService;
            this._storeMappingService = storeMappingService;
            this._storeService = storeService;
            this._workContext = workContext;
            this._sendInBlueEmailManager = sendInBlueEmailManager;
        }

        #endregion

        #region Utilities

        /// <summary>
        /// Prepare SendInBlueModel
        /// </summary>
        /// <param name="model">Model</param>
        protected void PrepareModel(SendInBlueModel model)
        {
            var storeId = GetActiveStoreScopeConfiguration(_storeService, _workContext);
            var sendInBlueSettings = _settingService.LoadSetting<SendInBlueSettings>(storeId);
            model.ActiveStoreScopeConfiguration = storeId;

            if (string.IsNullOrEmpty(sendInBlueSettings.ApiKey))
                return;

            //synchronization task
            var task = FindScheduledTask();
            if (task != null)
            {
                model.AutoSyncEachMinutes = task.Seconds / 60;
                model.AutoSync = task.Enabled;
            }

            //settings to model
            model.ApiKey = sendInBlueSettings.ApiKey;
            model.ListId = sendInBlueSettings.ListId;
            model.SMTPSenderId = sendInBlueSettings.SMTPSenderId;
            model.UseSMS = sendInBlueSettings.UseSMS;
            model.SMSFrom = sendInBlueSettings.SMSFrom;
            model.MyPhoneNumber = sendInBlueSettings.MyPhoneNumber;

            //check whether email account exist
            if (sendInBlueSettings.UseSendInBlueSMTP &&  _emailAccountService.GetEmailAccountById(sendInBlueSettings.SendInBlueEmailAccountId) != null)
                model.UseSendInBlueSMTP = sendInBlueSettings.UseSendInBlueSMTP;

            //overridable settings
            if (storeId > 0)
            {
                model.ListId_OverrideForStore = _settingService.SettingExists(sendInBlueSettings, x => x.ListId, storeId);
                model.UseSendInBlueSMTP_OverrideForStore = _settingService.SettingExists(sendInBlueSettings, x => x.UseSendInBlueSMTP, storeId);
                model.SMTPSenderId_OverrideForStore = _settingService.SettingExists(sendInBlueSettings, x => x.SMTPSenderId, storeId);
                model.UseSMS_OverrideForStore = _settingService.SettingExists(sendInBlueSettings, x => x.UseSMS, storeId);
                model.SMSFrom_OverrideForStore = _settingService.SettingExists(sendInBlueSettings, x => x.SMSFrom, storeId);
            }

            //get SendInBlue account info 
            var errors = string.Empty;
            var accountInfo = _sendInBlueEmailManager.GetAccountInfo(ref errors);
            if (string.IsNullOrEmpty(errors))
                model.AccountInfo = accountInfo;
            else
                ErrorNotification(errors);

            //check SMTP status
            if (!_sendInBlueEmailManager.SmtpIsEnabled(ref errors))
                ErrorNotification(errors);

            //get available lists of subscriptions for the synchronization from SendInBlue account
            model.AvailableLists = _sendInBlueEmailManager.GetLists();

            //get available senders of emails from SendInBlue account
            model.AvailableSenders = _sendInBlueEmailManager.GetSenders();
            
            //get message templates
            model.AvailableMessageTemplates = _messageTemplateService.GetAllMessageTemplates(storeId).Select(x => new SelectListItem
                {
                    Value = x.Id.ToString(),
                    Text = storeId > 0 ? x.Name : string.Format("{0} {1}", x.Name, !x.LimitedToStores ? string.Empty :
                        _storeService.GetAllStores().Where(s => _storeMappingService.GetStoresIdsWithAccess(x).Contains(s.Id))
                        .Aggregate("-", (current, next) => string.Format("{0} {1}, ", current, next.Name)).TrimEnd(','))
                }).ToList();

            //get string of allowed tokens
            model.AllowedTokens = _messageTokenProvider.GetListOfAllowedTokens()
                .Aggregate(string.Empty, (current, next) => string.Format("{0}, {1}", current, next)).Trim(',');
        }

        /// <summary>
        /// Get auto synchronization task
        /// </summary>
        /// <returns>Task</returns>
        protected ScheduleTask FindScheduledTask()
        {
            return _scheduleTaskService.GetTaskByType("Nop.Plugin.Misc.SendInBlue.Services.SendInBlueSynchronizationTask, Nop.Plugin.Misc.SendInBlue");
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
                tabName = this.Request.Form["selected-tab-name"];
            }

            if (!string.IsNullOrEmpty(tabName))
            {
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
        }

        #endregion

        #region Methods

        [ChildActionOnly]
        public ActionResult Configure()
        {
            var model = new SendInBlueModel();
            PrepareModel(model);

            return View("~/Plugins/Misc.SendInBlue/Views/SendInBlue/Configure.cshtml", model);
        }

        [ChildActionOnly]
        [HttpPost, ActionName("Configure")]
        [FormValueRequired("save")]
        public ActionResult Configure(SendInBlueModel model)
        {
            if (!ModelState.IsValid)
                return Configure();

            var storeId = GetActiveStoreScopeConfiguration(_storeService, _workContext);
            var sendInBlueSettings = _settingService.LoadSetting<SendInBlueSettings>(storeId);

            //set API key
            sendInBlueSettings.ApiKey = model.ApiKey;
            _settingService.SaveSetting(sendInBlueSettings, x => x.ApiKey, 0, false);

            //now clear settings cache
            _settingService.ClearCache();

            SuccessNotification(_localizationService.GetResource("Admin.Plugins.Saved"));

            return Configure();
        }

        [ChildActionOnly]
        [HttpPost, ActionName("Configure")]
        [FormValueRequired("saveSync")]
        public ActionResult SaveSynchronization(SendInBlueModel model)
        {
            if (!ModelState.IsValid)
                return Configure();

            var storeId = GetActiveStoreScopeConfiguration(_storeService, _workContext);
            var sendInBlueSettings = _settingService.LoadSetting<SendInBlueSettings>(storeId);

            //create or update synchronization task
            var task = FindScheduledTask();
            if (task != null)
            {
                task.Enabled = model.AutoSync;
                task.Seconds = model.AutoSyncEachMinutes * 60;
                _scheduleTaskService.UpdateTask(task);
            }
            else
                _scheduleTaskService.InsertTask(new ScheduleTask
                {
                    Name = "SendInBlue synchronization",
                    Seconds = model.AutoSyncEachMinutes * 60,
                    Enabled = model.AutoSync,
                    Type = "Nop.Plugin.Misc.SendInBlue.Services.SendInBlueSynchronizationTask, Nop.Plugin.Misc.SendInBlue",
                });
            if (model.AutoSync)
                SuccessNotification(_localizationService.GetResource("Plugins.Misc.SendInBlue.AutoSyncRestart"));

            //create store_id attribute (if not exists) in SendInBlue account
            _sendInBlueEmailManager.PrepareStoreAttribute();

            //set notify url for the importing process
            var currentStore = storeId == 0 ? _storeService.GetAllStores().FirstOrDefault() : _storeService.GetStoreById(storeId);
            sendInBlueSettings.UrlSync = string.Format("{0}{1}", currentStore.Url.TrimEnd('/'), Url.RouteUrl("Plugin.Misc.SendInBlue.ImportUsers"));
            _settingService.SaveSetting(sendInBlueSettings, x => x.UrlSync, storeId, false);

            //create webhook for the unsubscribing event
            var unsubscribeUrl = string.Format("{0}{1}", currentStore.Url.TrimEnd('/'), Url.RouteUrl("Plugin.Misc.SendInBlue.Unsubscribe"));
            sendInBlueSettings.UnsubscribeWebhookId = _sendInBlueEmailManager.GetUnsubscribeWebHookId(sendInBlueSettings.UnsubscribeWebhookId, unsubscribeUrl);
            _settingService.SaveSetting(sendInBlueSettings, x => x.UnsubscribeWebhookId, storeId, false);

            //set list for the synchronization
            sendInBlueSettings.ListId = model.ListId > 0 ? model.ListId : _sendInBlueEmailManager.CreateNewList(model.NewListName);
            _settingService.SaveSettingOverridablePerStore(sendInBlueSettings, x => x.ListId, model.ListId_OverrideForStore, storeId, false);

            //now clear settings cache
            _settingService.ClearCache();

            SuccessNotification(_localizationService.GetResource("Admin.Plugins.Saved"));

            //select "synchronization" tab
            SaveSelectedTabName("tab-synchronization");

            return Configure();
        }

        [ChildActionOnly]
        [HttpPost, ActionName("Configure")]
        [FormValueRequired("sync")]
        public ActionResult Synchronization(SendInBlueModel model)
        {
            if (!ModelState.IsValid)
                return Configure();

            var storeId = GetActiveStoreScopeConfiguration(_storeService, _workContext);

            //synchronize subscriptions for the certain store
            var syncResult = _sendInBlueEmailManager.Synchronize(true, storeId);
            if (string.IsNullOrEmpty(syncResult))
            {
                TempData["synchronizationStart"] = true;
                SuccessNotification(_localizationService.GetResource("Plugins.Misc.SendInBlue.ImportProcess"));
            }
            else
                ErrorNotification(syncResult);

            //select "synchronization" tab
            SaveSelectedTabName("tab-synchronization");

            return Configure();
        }

        public string GetSynchronizationInfo()
        {
            return TempData["synchronizationEnd"] != null ? TempData["synchronizationEnd"].ToString() : string.Empty;
        }

        [ChildActionOnly]
        [HttpPost, ActionName("Configure")]
        [FormValueRequired("saveSMTP")]
        public ActionResult ConfigureSMTP(SendInBlueModel model)
        {
            if (!ModelState.IsValid)
                return Configure();

            var storeId = GetActiveStoreScopeConfiguration(_storeService, _workContext);
            var sendInBlueSettings = _settingService.LoadSetting<SendInBlueSettings>(storeId);

            if (model.UseSendInBlueSMTP)
            {
                //set case invariant for true because tokens are used in uppercase format in SendInBlue's transactional emails 
                var messageTemplatesSettings = _settingService.LoadSetting<MessageTemplatesSettings>();
                messageTemplatesSettings.CaseInvariantReplacement = true;
                _settingService.SaveSetting(messageTemplatesSettings, x => x.CaseInvariantReplacement, 0, false);

                //check whether SMTP enabled on SendInBlue profile
                var errors = string.Empty;
                if (_sendInBlueEmailManager.SmtpIsEnabled(ref errors))
                {
                    //get email account or create new one
                    sendInBlueSettings.SendInBlueEmailAccountId = _sendInBlueEmailManager.GetEmailAccountId(_emailAccountService, model.SMTPSenderId, out errors);
                    if (string.IsNullOrEmpty(errors))
                        _settingService.SaveSetting(sendInBlueSettings, x => x.SendInBlueEmailAccountId, storeId, false);
                    else
                        ErrorNotification(errors);

                    //synchronize nopCommerce tokens and SendInBlue transactional attributes
                    _sendInBlueEmailManager.PrepareAttributes(_messageTokenProvider.GetListOfAllowedTokens(), out errors);
                    if (!string.IsNullOrEmpty(errors))
                        ErrorNotification(errors);
                }
                else
                {
                    //need to activate SMTP account
                    ErrorNotification(_localizationService.GetResource("Plugins.Misc.SendInBlue.ActivateSMTP"));
                    model.UseSendInBlueSMTP = false;
                }
            }

            //set whether to use SMTP of SendInBlue service
            sendInBlueSettings.UseSendInBlueSMTP = model.UseSendInBlueSMTP;
            _settingService.SaveSettingOverridablePerStore(sendInBlueSettings, x => x.UseSendInBlueSMTP, model.UseSendInBlueSMTP_OverrideForStore, storeId, false);

            //set sender of transactional emails
            sendInBlueSettings.SMTPSenderId = model.SMTPSenderId;
            _settingService.SaveSettingOverridablePerStore(sendInBlueSettings, x => x.SMTPSenderId, model.SMTPSenderId_OverrideForStore, storeId, false);

            //now clear settings cache
            _settingService.ClearCache();

            SuccessNotification(_localizationService.GetResource("Admin.Plugins.Saved"));

            //select "transactional" tab
            SaveSelectedTabName("tab-transactional");

            return Configure();
        }

        [HttpPost]
        public ActionResult MessageList(ListMessageModel model)
        {
            var storeId = GetActiveStoreScopeConfiguration(_storeService, _workContext);
            var messageTemplates = _messageTemplateService.GetAllMessageTemplates(storeId);

            var gridModel = new DataSourceResult
            {
                Data = messageTemplates.Select(x =>
                {
                    //standard template of message is edited in the admin area, SendInBlue template is edited in the SendInBlue account
                    var isStandardTemplate = !x.GetAttribute<bool>("SendInBlueTemplate", _genericAttributeService);
                    var message = new ListMessageModel
                    {
                        Id = x.Id,
                        Name = x.Name,
                        IsActive = x.IsActive,
                        ListOfStores = _storeService.GetAllStores().Where(s => !x.LimitedToStores || _storeMappingService.GetStoresIdsWithAccess(x).Contains(s.Id))
                            .Aggregate(string.Empty, (current, next) => string.Format("{0}, {1}", current, next.Name)).Trim(','),
                        TemplateTypeId = isStandardTemplate ? 0 : 1,
                        TemplateType = isStandardTemplate ? _localizationService.GetResource("Plugins.Misc.SendInBlue.StandardTemplate")
                            : _localizationService.GetResource("Plugins.Misc.SendInBlue.SendInBlueTemplate"),
                        EditLink = isStandardTemplate ? Url.Action("Edit", "MessageTemplate", new { id = x.Id, area = "Admin" })
                            : string.Format("{0}{1}", EDIT_TEMPLATE_URL, x.GetAttribute<int>("TemplateId", _genericAttributeService))
                    };

                    return message;
                }),
                Total = messageTemplates.Count
            };

            return Json(gridModel);
        }

        [HttpPost]
        public ActionResult MessageUpdate(ListMessageModel model)
        {
            if (!ModelState.IsValid)
                return Json(new DataSourceResult { Errors = ModelState.SerializeErrors() });

            var message = _messageTemplateService.GetMessageTemplateById(model.Id);

            //standard message template
            if (model.TemplateTypeId == 0)
            {
                _genericAttributeService.SaveAttribute(message, "SendInBlueTemplate", false);
                model.TemplateType = _localizationService.GetResource("Plugins.Misc.SendInBlue.StandardTemplate");
                model.EditLink = Url.Action("Edit", "MessageTemplate", new { id = model.Id, area = "Admin" });
            }

            //SendInBlue message template
            if (model.TemplateTypeId == 1)
            {
                var storeId = GetActiveStoreScopeConfiguration(_storeService, _workContext);
                var sendInBlueSettings = _settingService.LoadSetting<SendInBlueSettings>(storeId);

                //get template or create new one
                var templateId = _sendInBlueEmailManager.GetTemplateId(message.GetAttribute<int>("TemplateId", _genericAttributeService),
                    message, _emailAccountService.GetEmailAccountById(sendInBlueSettings.SendInBlueEmailAccountId));

                _genericAttributeService.SaveAttribute(message, "SendInBlueTemplate", true);
                _genericAttributeService.SaveAttribute(message, "TemplateId", templateId);
                model.TemplateType = _localizationService.GetResource("Plugins.Misc.SendInBlue.SendInBlueTemplate");
                model.EditLink = string.Format("{0}{1}", EDIT_TEMPLATE_URL, templateId);
            }

            //update nopCommerce message template
            if (model.IsActive != message.IsActive)
            {
                message.IsActive = model.IsActive;
                _messageTemplateService.UpdateMessageTemplate(message);
            }

            return new NullJsonResult();
        }

        [ChildActionOnly]
        [HttpPost, ActionName("Configure")]
        [FormValueRequired("saveSMS")]
        public ActionResult ConfigureSMS(SendInBlueModel model)
        {
            if (!ModelState.IsValid)
                return Configure();

            var storeId = GetActiveStoreScopeConfiguration(_storeService, _workContext);
            var sendInBlueSettings = _settingService.LoadSetting<SendInBlueSettings>(storeId);

            /* We do not clear cache after each setting update.
             * This behavior can increase performance because cached settings will not be cleared 
             * and loaded from database after each update */
            sendInBlueSettings.UseSMS = model.UseSMS;
            _settingService.SaveSettingOverridablePerStore(sendInBlueSettings, x => x.UseSMS, model.UseSMS_OverrideForStore, storeId, false);
            sendInBlueSettings.SMSFrom = model.SMSFrom;
            _settingService.SaveSettingOverridablePerStore(sendInBlueSettings, x => x.SMSFrom, model.SMSFrom_OverrideForStore, storeId, false);
            sendInBlueSettings.MyPhoneNumber = model.MyPhoneNumber;
            _settingService.SaveSetting(sendInBlueSettings, x => x.MyPhoneNumber, storeId, false);

            //now clear settings cache
            _settingService.ClearCache();

            SuccessNotification(_localizationService.GetResource("Admin.Plugins.Saved"));

            //select "sms" tab
            SaveSelectedTabName("tab-sms");

            return Configure();
        }

        [HttpPost]
        public ActionResult SMSList(ListSMSModel model)
        {
            var storeId = GetActiveStoreScopeConfiguration(_storeService, _workContext);
            var sendInBlueSettings = _settingService.LoadSetting<SendInBlueSettings>(storeId);

            //get message templates which are sending in SMS
            var messageTemplates = _messageTemplateService.GetAllMessageTemplates(storeId)
                .Where(x => sendInBlueSettings.SMSMessageTemplatesIds.Contains(x.Id)).ToList();
            var gridModel = new DataSourceResult
            {
                Data = messageTemplates.Select(x =>
                {
                    var phoneTypeID = x.GetAttribute<int>("PhoneTypeId", _genericAttributeService);
                    var sms = new ListSMSModel
                    {
                        Id = x.Id,
                        MessageId = x.Id,
                        Name = storeId > 0 ? x.Name : string.Format("{0} {1}", x.Name, !x.LimitedToStores ? string.Empty :
                            _storeService.GetAllStores().Where(s => !x.LimitedToStores || _storeMappingService.GetStoresIdsWithAccess(x).Contains(s.Id))
                            .Aggregate("-", (current, next) => string.Format("{0} {1}, ", current, next.Name)).TrimEnd(' ', ',')),
                        SMSActive = x.GetAttribute<bool>("UseSMS", _genericAttributeService),
                        PhoneTypeId = phoneTypeID,
                        Text = x.GetAttribute<string>("SMSText", _genericAttributeService)
                    };

                    //choose phone number for the sending SMS
                    //currently supported: "my phone" (filled on the configuration page), customer phone, phone of the billing address
                    switch (phoneTypeID)
                    {
                        case 0:
                            sms.PhoneType = _localizationService.GetResource("Plugins.Misc.SendInBlue.MyPhone");
                            break;
                        case 1:
                            sms.PhoneType = _localizationService.GetResource("Plugins.Misc.SendInBlue.CustomerPhone");
                            break;
                        case 2:
                            sms.PhoneType = _localizationService.GetResource("Plugins.Misc.SendInBlue.BillingAddressPhone");
                            break;
                        default:
                            break;
                    } 

                    return sms;
                }),
                Total = messageTemplates.Count
            };

            return Json(gridModel);
        }

        [HttpPost]
        public ActionResult SMSAdd(ListSMSModel model)
        {
            if (!ModelState.IsValid)
                return Json(new DataSourceResult { Errors = ModelState.SerializeErrors() });

            var storeId = GetActiveStoreScopeConfiguration(_storeService, _workContext);
            var sendInBlueSettings = _settingService.LoadSetting<SendInBlueSettings>(storeId);

            var message = _messageTemplateService.GetMessageTemplateById(model.MessageId);
            if (message != null)
            {
                _genericAttributeService.SaveAttribute(message, "UseSMS", model.SMSActive);
                _genericAttributeService.SaveAttribute(message, "SMSText", model.Text);
                _genericAttributeService.SaveAttribute(message, "PhoneTypeId", model.PhoneTypeId);
            }

            //update list of the message templates which are sending in SMS
            if (!sendInBlueSettings.SMSMessageTemplatesIds.Contains(model.MessageId))
            {
                sendInBlueSettings.SMSMessageTemplatesIds.Add(model.MessageId);
                _settingService.SaveSetting(sendInBlueSettings, x => x.SMSMessageTemplatesIds, storeId, false);
                _settingService.ClearCache();
            }

            return new NullJsonResult();
        }

        [HttpPost]
        public ActionResult SMSUpdate(ListSMSModel model)
        {
            if (!ModelState.IsValid)
                return Json(new DataSourceResult { Errors = ModelState.SerializeErrors() });

            SMSAdd(model);
            if (model.Id != model.MessageId)
                SMSDelete(new ListSMSModel { MessageId = model.Id });

            return new NullJsonResult();
        }

        [HttpPost]
        public ActionResult SMSDelete(ListSMSModel model)
        {
            if (!ModelState.IsValid)
                return Json(new DataSourceResult { Errors = ModelState.SerializeErrors() });

            var storeId = GetActiveStoreScopeConfiguration(_storeService, _workContext);
            var sendInBlueSettings = _settingService.LoadSetting<SendInBlueSettings>(storeId);

            //delete generic attributes
            var message = _messageTemplateService.GetMessageTemplateById(model.MessageId);
            if (message != null)
            {
                var attributes = _genericAttributeService.GetAttributesForEntity(message.Id, "MessageTemplate");
                var smsAttribute = attributes.FirstOrDefault(x => x.Key == "UseSMS");
                if (smsAttribute != null)
                    _genericAttributeService.DeleteAttribute(smsAttribute);
                smsAttribute = attributes.FirstOrDefault(x => x.Key == "SMSText");
                if (smsAttribute != null)
                    _genericAttributeService.DeleteAttribute(smsAttribute);
                smsAttribute = attributes.FirstOrDefault(x => x.Key == "PhoneTypeId");
                if (smsAttribute != null)
                    _genericAttributeService.DeleteAttribute(smsAttribute);
            }

            //update list of the message templates which are sending in SMS
            if (sendInBlueSettings.SMSMessageTemplatesIds.Contains(model.MessageId))
            {
                sendInBlueSettings.SMSMessageTemplatesIds.Remove(model.MessageId);
                _settingService.SaveSetting(sendInBlueSettings, x => x.SMSMessageTemplatesIds, storeId, false);
                _settingService.ClearCache();
            }

            return new NullJsonResult();
        }

        public ActionResult ImportUsers(FormCollection form)
        {
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

            return new HttpStatusCodeResult(HttpStatusCode.OK);
        }

        [HttpPost]
        public ActionResult UnsubscribeWebHook()
        {
            try
            {
                using (var streamReader = new StreamReader(_httpContext.Request.InputStream))
                {
                    _sendInBlueEmailManager.UnsubscribeWebhook(streamReader.ReadToEnd());
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex.Message, ex);
            }

            return new HttpStatusCodeResult(HttpStatusCode.OK);
        }

        #endregion
    }
}