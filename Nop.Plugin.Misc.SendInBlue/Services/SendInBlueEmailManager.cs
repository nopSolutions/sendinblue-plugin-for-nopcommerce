using Microsoft.CSharp;
using Newtonsoft.Json.Linq;
using SendInBlue;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Mvc;
using Nop.Core.Domain.Messages;
using Nop.Plugin.Misc.SendInBlue.Models;
using Nop.Services.Configuration;
using Nop.Services.Logging;
using Nop.Services.Messages;
using Nop.Services.Stores;

namespace Nop.Plugin.Misc.SendInBlue.Services
{
    public class SendInBlueEmailManager
    {
        #region Fields
        
        private readonly IEmailAccountService _emailAccountService;
        private readonly ILogger _logger;
        private readonly IMessageTokenProvider _messageTokenProvider;
        private readonly INewsLetterSubscriptionService _newsLetterSubscriptionService;
        private readonly ISettingService _settingService;
        private readonly IStoreService _storeService;

        #endregion

        #region Ctor

        public SendInBlueEmailManager(IEmailAccountService emailAccountService,
            ILogger logger,
            IMessageTokenProvider messageTokenProvider,
            INewsLetterSubscriptionService newsLetterSubscriptionService,
            ISettingService settingService,
            IStoreService storeService)
        {
            this._emailAccountService = emailAccountService;
            this._logger = logger;
            this._messageTokenProvider = messageTokenProvider;
            this._newsLetterSubscriptionService = newsLetterSubscriptionService;
            this._settingService = settingService;
            this._storeService = storeService;
        }

        #endregion

        private bool IsConfigured
        {
            get { return !string.IsNullOrEmpty(_settingService.LoadSetting<SendInBlueSettings>(0).ApiKey); }
        }

        private API _manager;
        private API Manager
        {
            get
            {
                if (_manager == null)
                    _manager = new API(_settingService.LoadSetting<SendInBlueSettings>(0).ApiKey);
                return _manager;
            }
        }

        private bool IsSuccess(dynamic request)
        {
            return request.code == "success" && request.data != null;
        }

        #region Methods

        public string Synchronize(bool manualSync = false, int storeScope = 0)
        {
            if (!IsConfigured)
            {
                _logger.Error("SendInBlue synchronization error: Plugin not configured");
                return "Plugin not configured";
            }

            if (manualSync)
                return SynchronizeOnStore(storeScope, _settingService.LoadSetting<SendInBlueSettings>(storeScope));

            SynchronizeOnStore(0, _settingService.LoadSetting<SendInBlueSettings>());
            var allStores = _storeService.GetAllStores();
            if (allStores.Count > 1)
                foreach (var store in allStores)
                {
                    var sendInBlueSettings = _settingService.LoadSetting<SendInBlueSettings>(store.Id);
                    if (_settingService.SettingExists(sendInBlueSettings, x => x.ListId, store.Id))
                        SynchronizeOnStore(store.Id, sendInBlueSettings);
                }

            return string.Empty;
        }

        private string SynchronizeOnStore(int storeId, SendInBlueSettings sendInBlueSettings)
        {
            var subscriptions = _newsLetterSubscriptionService.GetAllNewsLetterSubscriptions(storeId: storeId, isActive: true);
            if (subscriptions.Count == 0)
                return "There are no subscriptions";

            if (sendInBlueSettings.ListId == 0)
                return "List ID is empty";

            var csv = subscriptions.Aggregate(string.Format("{0};{1}", "EMAIL", "STORE_ID"),
                (current, next) => string.Format("{0}\n{1};{2}", current, next.Email, next.StoreId));
            var importParams = new Dictionary<string, object>
                {
                    { "notify_url", sendInBlueSettings.UrlSync },
                    { "body", csv },
                    { "listids", new List<int> { sendInBlueSettings.ListId } }
                };
            var import = Manager.import_users(importParams);
            if (!IsSuccess(import))
            {
                _logger.Error(string.Format("SendInBlue synchronization error: {0}", (string)import.message));
                return (string)import.message; 
            }

            return string.Empty;
        }

        public void Unsubscribe(string email)
        {
            if (!IsConfigured)
                _logger.Error("SendInBlue unsubscription error: Plugin not configured");

            var unsubscribeParams = new Dictionary<string, string> { { "email", email } };
            var unsubscribe = Manager.delete_user(unsubscribeParams);
            if (!IsSuccess(unsubscribe))
                _logger.Error(string.Format("SendInBlue unsubscription error: {0}", (string)unsubscribe.message));
        }

        public void UnsubscribeWebhook(string unsubscriberUser)
        {
            if (!IsConfigured)
                return;

            dynamic unsubscriber = JObject.Parse(unsubscriberUser);
            var email = (string)unsubscriber.email;
            var setting = _settingService.GetAllSettings().Where(
                x => x.Name == "sendinbluesettings.unsubscribewebhookid" &&
                x.Value == (string)unsubscriber.id).FirstOrDefault();
            var storeId = setting != null ? setting.StoreId : 0;
            var subscription = _newsLetterSubscriptionService.GetNewsLetterSubscriptionByEmailAndStoreId(email, storeId);
            if (subscription != null)
            {
                _newsLetterSubscriptionService.DeleteNewsLetterSubscription(subscription, false);
                _logger.Information(string.Format("SendInBlue unsubscription: email {0}, store {1}, date {2}", 
                    email, _storeService.GetStoreById(storeId).Name, (string)unsubscriber.date_event));
            }
        }

        public int SetUnsubscribeWebHook(string url, int webhookId)
        {
            if (!IsConfigured)
                return 0;

            if (webhookId != 0)
            {
                var existWebhookParams = new Dictionary<string, int> { { "id", webhookId } };
                var existWebHook = Manager.get_webhook(existWebhookParams);
                if (IsSuccess(existWebHook))
                    return webhookId;
            }

            var webhookParams = new Dictionary<string, object>
                {
                    { "url", url },
                    { "events", new List<string> { "unsubscribe" } },
                    { "is_plat", 1 }
                };
            var webHook = Manager.create_webhook(webhookParams);
            if (IsSuccess(webHook))
                return webHook.data.id;

            return 0;
        }

        public string GetAccountInfo(ref StringBuilder accountInfo)
        {
            if (!IsConfigured)
                return "Plugin not configured";

            var account = Manager.get_account();
            if (IsSuccess(account))
            {
                accountInfo.AppendFormat("Name: {0}<br />", HttpUtility.HtmlEncode(account.data[2].first_name));
                accountInfo.AppendFormat("Surname: {0}<br />", HttpUtility.HtmlEncode(account.data[2].last_name));
                accountInfo.AppendFormat("Plan: {0}<br />", account.data[0].plan_type);
                accountInfo.AppendFormat("Email credits: {0}<br />", account.data[0].credits);
                accountInfo.AppendFormat("SMS credits: {0}<br />", account.data[1].credits);
            }
            else
                return (string)account.message;

            return string.Empty;
        }

        public List<SelectListItem> GetLists()
        {
            var availableLists = new List<SelectListItem> { new SelectListItem { Text = "<New list>", Value = "0" } };
            if (!IsConfigured)
                return availableLists;

            var listsParams = new Dictionary<string, int> { { "list_parent", 0 }, { "page", 1 }, { "page_limit", 50 } };
            var lists = Manager.get_lists(listsParams);
            if (IsSuccess(lists))
                foreach (var list in lists.data.lists)
                    availableLists.Add(new SelectListItem { Text = list.name, Value = list.id });

            return availableLists;
        }

        public List<SelectListItem> GetSenders()
        {
            var availableSenders = new List<SelectListItem>();
            if (!IsConfigured)
                return availableSenders;

            var sendersParams = new Dictionary<string, string> { { "option", "" } };
            var senders = Manager.get_senders(sendersParams);
            if (IsSuccess(senders))
                foreach (var sender in senders.data)
                    availableSenders.Add(new SelectListItem
                    {
                        Text = string.Format("{0} ({1})", sender.from_name, sender.from_email),
                        Value = sender.id
                    });

            return availableSenders;
        }

        public Dictionary<DateTime, StatisticsModel> GetStatistics()
        {
            var statisticsDetails = new Dictionary<DateTime, StatisticsModel>();
            for (int i = 6; i >= 0; i--)
            {
                statisticsDetails.Add(DateTime.Today.AddDays(-i), new StatisticsModel());
            }
            
            if (!IsConfigured)
                return statisticsDetails;

            var statisticsParams = new Dictionary<string, object> { { "aggregate", 0 }, { "days", 7 } };
            var statistics = Manager.get_statistics(statisticsParams);
            if (IsSuccess(statistics) && statistics.data is JArray)
            {
                foreach (var day in statistics.data)
                {
                    statisticsDetails[(DateTime)day.date] = new StatisticsModel
                    {
                        Delivered = day.delivered,
                        Bounces = day.bounces,
                        Opens = day.opens,
                        Spam = day.spamreports
                    };
                }
            }

            return statisticsDetails.Take(7).ToDictionary(x => x.Key, x => x.Value);
        }

        public string SmtpEnabled()
        {
            if (!IsConfigured)
                return "Plugin not configured";

            var smtp = Manager.get_smtp_details();
            if (IsSuccess(smtp) && smtp.data.relay_data != null)
                if (smtp.data.relay_data.status == "enabled")
                    return string.Empty;
                else
                    return (string)smtp.data.relay_data.status;
            else
                return (string)smtp.message;
        }

        public int PrepareList(string name)
        {
            if (!IsConfigured)
                return 0;

            var allFoldersParams = new Dictionary<string, int> { { "page", 1 }, { "page_limit", 50 } };
            var allFolders = Manager.get_folders(allFoldersParams);
            if (!IsSuccess(allFolders))
                return 0;

            var folder = (allFolders.data.folders as JArray).FirstOrDefault(x => (string)x["name"] == "nopCommerce");
            var folderId = 0;
            if (folder == null)
            {
                var newFolderParams = new Dictionary<string, string> { { "name", "nopCommerce" } };
                var newFolder = Manager.create_folder(newFolderParams);
                if (!IsSuccess(newFolder))
                    return 0;

                folderId = newFolder.data.id;
            }
            else
                folderId = (int)folder["id"];

            var newListParams = new Dictionary<string, object>
            {
                { "list_name", name },
                { "list_parent", folderId }
            };
            var newList = Manager.create_list(newListParams);
            if (!IsSuccess(newList))
                return 0;

            return newList.data.id;
        }

        public void PrepareStoreAttribute()
        {
            if (!IsConfigured)
                return;

            var attributeParams = new Dictionary<string, string> { { "type", "normal" } };
            var attribute = Manager.get_attribute(attributeParams);
            if (!IsSuccess(attribute))
                return;

            if ((attribute.data as JArray).Any(x => (string)x["name"] == "STORE_ID"))
                return;
            
            var storeAttributeParams = new Dictionary<string, object>
            {
                { "type", "normal" },
                { "data", new Dictionary<string, string> { { "STORE_ID", "text" } } }
            };
            var storeAttribute = Manager.create_attribute(storeAttributeParams);
        }

        public string PrepareEmailAccount(string senderId, SendInBlueSettings sendInBlueSettings)
        {
            if (!IsConfigured)
                return string.Empty;

            var sendersParams = new Dictionary<string, string> { { "option", "" } };
            var senders = Manager.get_senders(sendersParams);
            if (!IsSuccess(senders))
                return (string)senders.message;

            foreach (var sender in senders.data)
            {
                if (sender.id == senderId)
                {
                    var emailAccount = _emailAccountService.GetAllEmailAccounts().FirstOrDefault(x =>
                        x.DisplayName == (string)sender.from_name && x.Email == (string)sender.from_email);
                    if (emailAccount != null)
                    {
                        sendInBlueSettings.SendInBlueEmailAccountId = emailAccount.Id;
                        break;
                    }

                    var smtp = Manager.get_smtp_details();
                    if (!IsSuccess(smtp))
                        return (string)smtp.message;

                    var sendInBlueEmailAccount = new EmailAccount()
                    {
                        Host = smtp.data.relay_data.data.relay,
                        Port = smtp.data.relay_data.data.port,
                        Username = smtp.data.relay_data.data.username,
                        Password = smtp.data.relay_data.data.password,
                        EnableSsl = true,
                        Email = sender.from_email,
                        DisplayName = sender.from_name
                    };
                    _emailAccountService.InsertEmailAccount(sendInBlueEmailAccount);
                    sendInBlueSettings.SendInBlueEmailAccountId = sendInBlueEmailAccount.Id;
                    break;
                }
            }

            return string.Empty;
        }

        public string PrepareAttributes()
        {
            if (!IsConfigured)
                return string.Empty;

            var attributesParams = new Dictionary<string, string> { { "type", "transactional" } };
            var attributes = Manager.get_attribute(attributesParams);
            if (!IsSuccess(attributes))
                return (string)attributes.message;

            var tokens = _messageTokenProvider.GetListOfAllowedTokens().Select(x => x.Replace("%", "").Replace(".", "_").Replace("(s)","-s-").ToUpperInvariant());
            if (attributes.data[0] != null)
                tokens = tokens.Except((attributes.data[0] as JArray).Select(x => x["name"].ToString()).ToList());
            var notExistsAttributes = tokens.ToDictionary(x => x, x => "text");
            if (notExistsAttributes.Count > 0)
            {
                notExistsAttributes.Add("ID", "id");
                var newAttributeParams = new Dictionary<string, object> { { "type", "transactional" }, { "data", notExistsAttributes } };
                var newAttributes = Manager.create_attribute(newAttributeParams);
                if (!IsSuccess(newAttributes))
                    return (string)newAttributes.message;
            }
            return string.Empty;
        }

        public int TemplateExistsId(int id)
        {
            if (!IsConfigured)
                return 0;

            var templateParams = new Dictionary<string, int> { { "id", id } };
            var template = Manager.get_campaign_v2(templateParams);
            if (IsSuccess(template))
                return id;
            return 0;
        }
        
        public int SetNewTemplate(MessageTemplate message, SendInBlueSettings sendInBlueSettings)
        {
            if (!IsConfigured)
                return 0;

            var emailAccount = _emailAccountService.GetEmailAccountById(sendInBlueSettings.SendInBlueEmailAccountId);
            if (emailAccount != null)
            {
                var body = Regex.Replace(message.Body, "(%[^\\%]*.%)", x => x.ToString().Replace(".", "_").Replace("(s)", "-s-").ToUpperInvariant());
                var subject = Regex.Replace(message.Subject, "(%[^\\%]*.%)", x => x.ToString().Replace(".", "_").Replace("(s)", "-s-").ToUpperInvariant());
                var newTemplateParams = new Dictionary<string, object>
                        {
                            { "from_name", emailAccount.DisplayName },
                            { "from_email", emailAccount.Email },
                            { "template_name", message.Name },
                            { "subject", subject },
                            { "html_content", body },
                            { "status", 1 }
                        };
                var newTemplate = Manager.create_template(newTemplateParams);
                if (IsSuccess(newTemplate))
                    return newTemplate.data.id;
            }
            return 0;
        }

        public QueuedEmail GetQueuedEmailFromTemplate(int templateId)
        {
            if (!IsConfigured)
            {
                _logger.Error("SendInBlue email sending error: Plugin not configured");
                return null;
            }

            if (templateId == 0)
            {
                _logger.Error("SendInBlue email sending error: Message template is empty");
                return null;
            }

            var templateParams = new Dictionary<string, int> { { "id", templateId } };
            var template = Manager.get_campaign_v2(templateParams);
            if (!IsSuccess(template))
            {
                _logger.Error(string.Format("SendInBlue email sending error: {0}", (string)template.message));
                return null;
            }

            var subject = Regex.Replace((string)template.data[0].subject, "(%[^_]*_.*%)", x => x.ToString().Replace("_", ".").Replace("-S-", "(s)"));
            var body = Regex.Replace((string)template.data[0].html_content, "(%[^_]*_.*%)", x => x.ToString().Replace("_", ".").Replace("-S-", "(s)"));

            return new QueuedEmail
            {
                Subject = subject,
                Body = body,
                FromName = template.data[0].from_name,
                From = template.data[0].from_email
            };
        }

        public void SendSMS(string to, string from, string text)
        {
            if (!IsConfigured)
                return;

            if (string.IsNullOrEmpty(to) || string.IsNullOrEmpty(text))
            {
                _logger.Error("SendInBlue SMS sending error: Phone number or SMS text is empty");
                return;
            }

            var smsParams = new Dictionary<string, string>
            {
                { "to", to },
                { "from", from },
                { "text", text },
                { "type", "transactional" }
            };
            var sms = Manager.send_sms(smsParams);
            if (IsSuccess(sms))
                _logger.Information(string.Format("SendInBlue SMS sent: {0}", (string)sms.data.description ??
                    string.Format("credits remaining {0}", (string)sms.data.remaining_credit)));
            else
                _logger.Error(string.Format("SendInBlue SMS sending error: {0}", (string)sms.message));
        }

        #endregion
    }
}
