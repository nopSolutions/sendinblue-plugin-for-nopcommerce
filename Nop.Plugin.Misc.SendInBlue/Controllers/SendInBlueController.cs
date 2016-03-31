using System;
using System.Linq;
using System.IO;
using System.Text;
using System.Web;
using System.Web.Helpers;
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
        private const string PATH_VIEW = "~/Plugins/Misc.SendInBlue/Views/SendInBlue/Configure.cshtml";

        #region fields

        private readonly EmailAccountSettings _emailAccountSettings;
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

        #region ctor

        public SendInBlueController(EmailAccountSettings emailAccountSettings,
            HttpContextBase httpContext,
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
            this._emailAccountSettings = emailAccountSettings;
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
        /// <param name="storeId">Store id</param>
        private void PrepareModel(SendInBlueModel model, int storeId)
        {
            ScheduleTask task = FindScheduledTask();
            if (task != null)
            {
                model.AutoSyncEachMinutes = task.Seconds / 60;
                model.AutoSync = task.Enabled;
            }

            var sendInBlueSettings = _settingService.LoadSetting<SendInBlueSettings>(storeId);
            model.ActiveStoreScopeConfiguration = storeId;
            if (string.IsNullOrEmpty(sendInBlueSettings.ApiKey))
                return;

            model.ApiKey = sendInBlueSettings.ApiKey;
            model.ListId = sendInBlueSettings.ListId;
            model.SMTPSenderId = sendInBlueSettings.SMTPSenderId;
            model.UseSMS = sendInBlueSettings.UseSMS;
            model.SMSFrom = sendInBlueSettings.SMSFrom;
            model.MyPhoneNumber = sendInBlueSettings.MyPhoneNumber;
            if (sendInBlueSettings.UseSendInBlueSMTP &&  _emailAccountService.GetEmailAccountById(sendInBlueSettings.SendInBlueEmailAccountId) != null)
                model.UseSendInBlueSMTP = sendInBlueSettings.UseSendInBlueSMTP;
            if (storeId > 0)
            {
                model.ListId_OverrideForStore = _settingService.SettingExists(sendInBlueSettings, x => x.ListId, storeId);
                model.UseSendInBlueSMTP_OverrideForStore = _settingService.SettingExists(sendInBlueSettings, x => x.UseSendInBlueSMTP, storeId);
                model.SMTPSenderId_OverrideForStore = _settingService.SettingExists(sendInBlueSettings, x => x.SMTPSenderId, storeId);
                model.UseSMS_OverrideForStore = _settingService.SettingExists(sendInBlueSettings, x => x.UseSMS, storeId);
                model.SMSFrom_OverrideForStore = _settingService.SettingExists(sendInBlueSettings, x => x.SMSFrom, storeId);
                model.MyPhoneNumber_OverrideForStore = _settingService.SettingExists(sendInBlueSettings, x => x.MyPhoneNumber, storeId);
            }

            var accountInfo = new StringBuilder();
            var errors = _sendInBlueEmailManager.GetAccountInfo(ref accountInfo);
            if (!string.IsNullOrEmpty(errors))
                ErrorNotification(errors);
            errors = _sendInBlueEmailManager.SmtpEnabled();
            if (!string.IsNullOrEmpty(errors))
                ErrorNotification(errors);
            model.AccountInfo = accountInfo.ToString();
            model.AvailableLists = _sendInBlueEmailManager.GetLists();
            model.AvailableSenders = _sendInBlueEmailManager.GetSenders();
            
            model.AvailableMessageTemplates = _messageTemplateService.GetAllMessageTemplates(storeId).Select(x => new SelectListItem
                {
                    Value = x.Id.ToString(),
                    Text = storeId > 0 ? x.Name : string.Format("{0} {1}", x.Name, !x.LimitedToStores ? string.Empty :
                        _storeService.GetAllStores().Where(s => !x.LimitedToStores || _storeMappingService.GetStoresIdsWithAccess(x).Contains(s.Id))
                        .Aggregate("-", (current, next) => string.Format("{0} {1}, ", current, next.Name)).TrimEnd(','))
                }).ToList();

            model.AllowedTokens = _messageTokenProvider.GetListOfAllowedTokens()
                .Aggregate(string.Empty, (current, next) => string.Format("{0}, {1}", current, next)).Trim(',');
        }

        /// <summary>
        /// Get auto synchronization task
        /// </summary>
        /// <returns>Task</returns>
        private ScheduleTask FindScheduledTask()
        {
            return _scheduleTaskService.GetTaskByType("Nop.Plugin.Misc.SendInBlue.Services.SendInBlueSynchronizationTask, Nop.Plugin.Misc.SendInBlue");
        }

        public string GetSynchronizationInfo()
        {
            if (TempData["synchronizationEnd"] == null)
                return string.Empty;
            return TempData["synchronizationEnd"].ToString();
        }

        /// <summary>
        /// Save selected TAB index
        /// </summary>
        /// <param name="index">Index to save; null to automatically detect it</param>
        /// <param name="persistForTheNextRequest">A value indicating whether a message should be persisted for the next request</param>
        private void SaveSelectedTabIndex(int? index = null, bool persistForTheNextRequest = true)
        {
            //keep this method synchronized with
            //"GetSelectedTabIndex" method of \Nop.Web.Framework\ViewEngines\Razor\WebViewPage.cs
            if (!index.HasValue)
            {
                int tmp;
                if (int.TryParse(this.Request.Form["selected-tab-index"], out tmp))
                    index = tmp;
            }
            if (index.HasValue)
            {
                string dataKey = "nop.selected-tab-index";
                if (persistForTheNextRequest)
                    TempData[dataKey] = index;
                else
                    ViewData[dataKey] = index;
            }
        }

        #endregion

        #region Methods

        [ChildActionOnly]
        public ActionResult Configure()
        {
            var model = new SendInBlueModel();
            PrepareModel(model, this.GetActiveStoreScopeConfiguration(_storeService, _workContext));

            return View(PATH_VIEW, model);
        }

        [ChildActionOnly]
        [HttpPost, ActionName("Configure")]
        [FormValueRequired("save")]
        public ActionResult Configure(SendInBlueModel model)
        {
            if (!ModelState.IsValid)
                return Configure();

            var storeId = this.GetActiveStoreScopeConfiguration(_storeService, _workContext);
            var sendInBlueSettings = _settingService.LoadSetting<SendInBlueSettings>(storeId);
            sendInBlueSettings.ApiKey = model.ApiKey;
            _settingService.SaveSetting(sendInBlueSettings, x => x.ApiKey, 0, false);
            _settingService.ClearCache();

            PrepareModel(model, storeId);
            SuccessNotification(_localizationService.GetResource("Admin.Plugins.Saved"));
            SaveSelectedTabIndex(0);

            return View(PATH_VIEW, model);
        }

        public ActionResult Statistics()
        {
            var cacheKey = "STATISTICS_CACHE_KEY";
            var chart = Chart.GetFromCache(cacheKey);
            if (chart == null)
            {
                var statistics = _sendInBlueEmailManager.GetStatistics();
                chart = new Chart(800, 400, ChartTheme.Vanilla).AddTitle("Statistics of transactional emails").AddLegend();
                var xVal = statistics.Keys.Select(x => x.ToShortDateString()).ToList();
                foreach (var item in typeof(StatisticsModel).GetProperties())
                {
                    var yVal = statistics.Values.Select(x => (int?)item.GetValue(x) ?? 0).ToList();
                    chart.AddSeries(name: item.Name, chartType: "Line", xValue: xVal, yValues: yVal);
                }
                chart.SaveToCache(cacheKey, 10, false);
            }
            return File(chart.GetBytes("png"), "image/png");
        }

        [ChildActionOnly]
        [HttpPost, ActionName("Configure")]
        [FormValueRequired("saveSync")]
        public ActionResult SaveSynchronization(SendInBlueModel model)
        {
            if (!ModelState.IsValid)
                return Configure();

            var saveInfo = string.Empty;
            var task = FindScheduledTask();
            if (task != null)
            {
                task.Enabled = model.AutoSync;
                task.Seconds = model.AutoSyncEachMinutes * 60;
                _scheduleTaskService.UpdateTask(task);
                saveInfo = _localizationService.GetResource("Plugins.Misc.SendInBlue.AutoSyncRestart");
            }

            var storeId = this.GetActiveStoreScopeConfiguration(_storeService, _workContext);
            var sendInBlueSettings = _settingService.LoadSetting<SendInBlueSettings>(storeId);
            var webHookId = storeId == 0 ? _settingService.LoadSetting<SendInBlueSettings>(0).UnsubscribeWebhookId : sendInBlueSettings.UnsubscribeWebhookId;
            var currentStore = storeId == 0 ? _storeService.GetAllStores().FirstOrDefault() : _storeService.GetStoreById(storeId);

            sendInBlueSettings.AutoSync = model.AutoSync;
            sendInBlueSettings.AutoSyncEachMinutes = model.AutoSyncEachMinutes;
            sendInBlueSettings.UrlSync = string.Format("{0}{1}", currentStore.Url.TrimEnd('/'), Url.RouteUrl("Plugin.Misc.SendInBlue.ImportUsers"));
            sendInBlueSettings.UnsubscribeWebhookId = _sendInBlueEmailManager.SetUnsubscribeWebHook(string.Format("{0}{1}", currentStore.Url.TrimEnd('/'), 
                Url.RouteUrl("Plugin.Misc.SendInBlue.Unsubscribe")), webHookId);
            if (model.ListId_OverrideForStore || storeId == 0)
            {
                if (model.ListId == 0)
                {
                    sendInBlueSettings.ListId = _sendInBlueEmailManager.PrepareList(model.NewListName);
                    model.NewListName = null;
                }
                else
                    sendInBlueSettings.ListId = model.ListId;
                _settingService.SaveSetting(sendInBlueSettings, x => x.ListId, storeId, false);
            }
            else if (storeId > 0)
                _settingService.DeleteSetting(sendInBlueSettings, x => x.ListId, storeId);
            _settingService.SaveSetting(sendInBlueSettings, x => x.AutoSync, 0, false);
            _settingService.SaveSetting(sendInBlueSettings, x => x.AutoSyncEachMinutes, 0, false);
            _settingService.SaveSetting(sendInBlueSettings, x => x.UrlSync, 0, false);
            _settingService.SaveSetting(sendInBlueSettings, x => x.UnsubscribeWebhookId, storeId, false);
            _settingService.ClearCache();

            _sendInBlueEmailManager.PrepareStoreAttribute();
            PrepareModel(model, storeId);
            if (string.IsNullOrEmpty(saveInfo))
                SuccessNotification(_localizationService.GetResource("Admin.Plugins.Saved"));
            else
                SuccessNotification(saveInfo);
            SaveSelectedTabIndex(1);

            return View(PATH_VIEW, model);
        }

        [ChildActionOnly]
        [HttpPost, ActionName("Configure")]
        [FormValueRequired("sync")]
        public ActionResult Synchronization(SendInBlueModel model)
        {
            if (!ModelState.IsValid)
                return Configure();

            var storeId = this.GetActiveStoreScopeConfiguration(_storeService, _workContext);
            var syncResult = _sendInBlueEmailManager.Synchronize(true, storeId);
            if (string.IsNullOrEmpty(syncResult))
            {
                model.SynchronizationInfo = _localizationService.GetResource("Plugins.Misc.SendInBlue.ImportProcess");
                TempData["synchronizationStart"] = true;
            }
            else
                ErrorNotification(syncResult);

            PrepareModel(model, storeId);
            SaveSelectedTabIndex(1);

            return View(PATH_VIEW, model);
        }
            
        [ChildActionOnly]
        [HttpPost, ActionName("Configure")]
        [FormValueRequired("saveSMTP")]
        public ActionResult ConfigureSMTP(SendInBlueModel model)
        {
            if (!ModelState.IsValid)
                return Configure();

            var storeId = this.GetActiveStoreScopeConfiguration(_storeService, _workContext);
            var sendInBlueSettings = _settingService.LoadSetting<SendInBlueSettings>(storeId);

            if (model.UseSendInBlueSMTP)
            {
                var messageTemplatesSettings = _settingService.LoadSetting<MessageTemplatesSettings>();
                messageTemplatesSettings.CaseInvariantReplacement = true;
                _settingService.SaveSetting(messageTemplatesSettings, x => x.CaseInvariantReplacement, 0, false);
            }

            if (model.SMTPSenderId_OverrideForStore || storeId == 0)
            {
                if (string.IsNullOrEmpty(_sendInBlueEmailManager.SmtpEnabled()))
                {
                    var errors = _sendInBlueEmailManager.PrepareEmailAccount(model.SMTPSenderId, sendInBlueSettings);
                    if (!string.IsNullOrEmpty(errors))
                        ErrorNotification(errors);
                    errors = _sendInBlueEmailManager.PrepareAttributes();
                    if (!string.IsNullOrEmpty(errors))
                        ErrorNotification(errors);
                }
                else
                {
                    ErrorNotification(_localizationService.GetResource("Plugins.Misc.SendInBlue.ActivateSMTP"));
                    model.UseSendInBlueSMTP = false;
                }
                sendInBlueSettings.SMTPSenderId = model.SMTPSenderId;
                _settingService.SaveSetting(sendInBlueSettings, x => x.SMTPSenderId, storeId, false);
                _settingService.SaveSetting(sendInBlueSettings, x => x.SendInBlueEmailAccountId, storeId, false);
            }
            else if (storeId > 0)
                _settingService.DeleteSetting(sendInBlueSettings, x => x.SMTPSenderId, storeId);

            if (model.UseSendInBlueSMTP_OverrideForStore || storeId == 0)
            {
                sendInBlueSettings.UseSendInBlueSMTP = model.UseSendInBlueSMTP;
                _settingService.SaveSetting(sendInBlueSettings, x => x.UseSendInBlueSMTP, storeId, false);
            }
            else if (storeId > 0)
                _settingService.DeleteSetting(sendInBlueSettings, x => x.UseSendInBlueSMTP, storeId);
            _settingService.ClearCache();

            PrepareModel(model, storeId);
            SuccessNotification(_localizationService.GetResource("Admin.Plugins.Saved"));
            SaveSelectedTabIndex(2);

            return View(PATH_VIEW, model);
        }

        [HttpPost]
        public ActionResult MessageList(ListMessageModel model)
        {
            var storeId = this.GetActiveStoreScopeConfiguration(_storeService, _workContext);
            var messageTemplates = _messageTemplateService.GetAllMessageTemplates(storeId);
            var gridModel = new DataSourceResult
            {
                Data = messageTemplates.Select(x =>
                {
                    var standartTemplate = !x.GetAttribute<bool>("SendInBlueTemplate", _genericAttributeService);
                    var message = new ListMessageModel
                    {
                        Id = x.Id,
                        Name = x.Name,
                        IsActive = x.IsActive,
                        ListOfStores = _storeService.GetAllStores().Where(s => !x.LimitedToStores || _storeMappingService.GetStoresIdsWithAccess(x).Contains(s.Id))
                            .Aggregate(string.Empty, (current, next) => string.Format("{0}, {1}", current, next.Name)).Trim(','),
                        TemplateTypeId = standartTemplate ? 0 : 1,
                        TemplateType = standartTemplate ? _localizationService.GetResource("Plugins.Misc.SendInBlue.StandartTemplate")
                            : _localizationService.GetResource("Plugins.Misc.SendInBlue.SendInBlueTemplate")
                    };
                    if (standartTemplate)
                        message.EditLink = Url.Action("Edit", "MessageTemplate", new { id = x.Id, area = "Admin" });
                    else
                        message.EditLink = "https://my.sendinblue.com/camp/step1/type/template/id/" + x.GetAttribute<int>("TemplateId", _genericAttributeService);
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
            if (model.TemplateTypeId == 0)
            {
                _genericAttributeService.SaveAttribute(message, "SendInBlueTemplate", false);
                model.TemplateType = _localizationService.GetResource("Plugins.Misc.SendInBlue.StandartTemplate");
                model.EditLink = Url.Action("Edit", "MessageTemplate", new { id = model.Id, area = "Admin" });
            }

            if (model.TemplateTypeId == 1)
            {
                var storeId = this.GetActiveStoreScopeConfiguration(_storeService, _workContext);
                var sendInBlueSettings = _settingService.LoadSetting<SendInBlueSettings>(storeId);
                var templateId = message.GetAttribute<int>("TemplateId", _genericAttributeService);
                if (templateId != 0)
                    templateId = _sendInBlueEmailManager.TemplateExistsId(templateId);
                if (templateId == 0)
                    templateId = _sendInBlueEmailManager.SetNewTemplate(message, sendInBlueSettings);

                _genericAttributeService.SaveAttribute(message, "SendInBlueTemplate", true);
                _genericAttributeService.SaveAttribute(message, "TemplateId", templateId);
                model.TemplateType = _localizationService.GetResource("Plugins.Misc.SendInBlue.SendInBlueTemplate");
                model.EditLink = "https://my.sendinblue.com/camp/step1/type/template/id/" + templateId;
            }

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

            var storeId = this.GetActiveStoreScopeConfiguration(_storeService, _workContext);
            var sendInBlueSettings = _settingService.LoadSetting<SendInBlueSettings>(storeId);
            
            if (model.UseSMS_OverrideForStore || storeId == 0)
            {
                sendInBlueSettings.UseSMS = model.UseSMS;
                _settingService.SaveSetting(sendInBlueSettings, x => x.UseSMS, storeId, false);
            }
            else if (storeId > 0)
                _settingService.DeleteSetting(sendInBlueSettings, x => x.UseSMS, storeId);
            if (model.SMSFrom_OverrideForStore || storeId == 0)
            {
                sendInBlueSettings.SMSFrom = model.SMSFrom;
                _settingService.SaveSetting(sendInBlueSettings, x => x.SMSFrom, storeId, false);
            }
            else if (storeId > 0)
                _settingService.DeleteSetting(sendInBlueSettings, x => x.SMSFrom, storeId);
            if (model.MyPhoneNumber_OverrideForStore || storeId == 0)
            {
                sendInBlueSettings.MyPhoneNumber = model.MyPhoneNumber;
                _settingService.SaveSetting(sendInBlueSettings, x => x.MyPhoneNumber, storeId, false);
            }
            else if (storeId > 0)
                _settingService.DeleteSetting(sendInBlueSettings, x => x.MyPhoneNumber, storeId);
            _settingService.ClearCache();

            PrepareModel(model, storeId);
            SuccessNotification(_localizationService.GetResource("Admin.Plugins.Saved"));
            SaveSelectedTabIndex(3);

            return View(PATH_VIEW, model);
        }

        [HttpPost]
        public ActionResult SMSList(ListSMSModel model)
        {
            var storeId = this.GetActiveStoreScopeConfiguration(_storeService, _workContext);
            var sendInBlueSettings = _settingService.LoadSetting<SendInBlueSettings>(storeId);
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

            var message = _messageTemplateService.GetMessageTemplateById(model.MessageId);
            _genericAttributeService.SaveAttribute(message, "UseSMS", model.SMSActive);
            _genericAttributeService.SaveAttribute(message, "SMSText", model.Text);
            _genericAttributeService.SaveAttribute(message, "PhoneTypeId", model.PhoneTypeId);

            var storeId = this.GetActiveStoreScopeConfiguration(_storeService, _workContext);
            var sendInBlueSettings = _settingService.LoadSetting<SendInBlueSettings>(storeId);
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

            var message = _messageTemplateService.GetMessageTemplateById(model.MessageId);
            var attribures = _genericAttributeService.GetAttributesForEntity(message.Id, "MessageTemplate");
            var smsAttribute = attribures.FirstOrDefault(x => x.Key == "UseSMS");
            if (smsAttribute != null)
                _genericAttributeService.DeleteAttribute(smsAttribute);
            smsAttribute = attribures.FirstOrDefault(x => x.Key == "SMSText");
            if (smsAttribute != null)
                _genericAttributeService.DeleteAttribute(smsAttribute);
            smsAttribute = attribures.FirstOrDefault(x => x.Key == "PhoneTypeId");
            if (smsAttribute != null)
                _genericAttributeService.DeleteAttribute(smsAttribute);

            var storeId = this.GetActiveStoreScopeConfiguration(_storeService, _workContext);
            var sendInBlueSettings = _settingService.LoadSetting<SendInBlueSettings>(storeId);
            if (sendInBlueSettings.SMSMessageTemplatesIds.Contains(model.MessageId))
            {
                sendInBlueSettings.SMSMessageTemplatesIds.Remove(model.MessageId);
                _settingService.SaveSetting(sendInBlueSettings, x => x.SMSMessageTemplatesIds, storeId, false);
                _settingService.ClearCache();
            }

            return new NullJsonResult();
        }

        [HttpPost]
        public ActionResult ImportUsers(FormCollection form)
        {
            try
            {
                var logInfo = string.Format("SendInBlue synchronization: New emails {0}, Existing emails {1}, Invalid emails {2}, Duplicates emails {3}",
                    form["new_emails"], form["emails_exists"], form["invalid_email"], form["duplicates_email"]);
                _logger.Information(logInfo);
                var syncInfo = new StringBuilder("<b>SendInBlue synchronization</b><br />");
                syncInfo.AppendFormat("New emails: {0}<br />", form["new_emails"]);
                syncInfo.AppendFormat("Existing emails: {0}<br />", form["emails_exists"]);
                syncInfo.AppendFormat("Invalid emails: {0}<br />", form["invalid_email"]);
                syncInfo.AppendFormat("Duplicates emails: {0}<br />", form["duplicates_email"]);
                TempData["synchronizationEnd"] = syncInfo.ToString();
            }
            catch (Exception ex)
            {
                _logger.Error(ex.Message, ex);
                TempData["synchronizationEnd"] = ex.Message;
                return Content("Bad request");
            }
            return Content("OK");
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
                return Content("Bad request");
            }
            return Content("OK");
        }

        #endregion
    }
}