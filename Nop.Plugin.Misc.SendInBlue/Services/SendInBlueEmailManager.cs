using mailinblue;
using Microsoft.CSharp;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Mvc;
using Nop.Core.Domain.Messages;
using Nop.Services.Configuration;
using Nop.Services.Logging;
using Nop.Services.Messages;
using Nop.Services.Stores;

namespace Nop.Plugin.Misc.SendInBlue.Services
{
    public class SendInBlueEmailManager
    {
        #region Fields
        
        private readonly ILogger _logger;
        private readonly INewsLetterSubscriptionService _newsLetterSubscriptionService;
        private readonly ISettingService _settingService;
        private readonly IStoreService _storeService;

        #endregion

        #region Ctor

        public SendInBlueEmailManager(ILogger logger,
            INewsLetterSubscriptionService newsLetterSubscriptionService,
            ISettingService settingService,
            IStoreService storeService)
        {
            this._logger = logger;
            this._newsLetterSubscriptionService = newsLetterSubscriptionService;
            this._settingService = settingService;
            this._storeService = storeService;
        }

        #endregion

        #region Private properties

        /// <summary>
        /// Gets a value indicating whether API key is specified
        /// </summary>
        private bool IsConfigured
        {
            get { return !string.IsNullOrEmpty(_settingService.LoadSetting<SendInBlueSettings>().ApiKey); }
        }

        
        private API _manager;
        /// <summary>
        /// Get single manager for the requesting service
        /// </summary>
        private API Manager
        {
            get
            {
                if (_manager == null)
                    _manager = new API(_settingService.LoadSetting<SendInBlueSettings>().ApiKey);
                return _manager;
            }
        }

        /// <summary>
        /// Gets a value indicating whether request was succeeded
        /// </summary>
        private bool IsSuccess(dynamic request)
        {
            return request.code == "success" && request.data != null;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Import subscriptions from nopCommerce to SendInBlue
        /// </summary>
        /// <param name="manualSync">A value indicating that method is called by user</param>
        /// <param name="storeScope">Store identifier; pass 0 for the synchronization for the all stores</param>
        /// <returns>Empty string if success, otherwise error string</returns>
        public string Synchronize(bool manualSync = false, int storeScope = 0)
        {
            var error = string.Empty;
            if (!IsConfigured)
            {
                _logger.Error("SendInBlue synchronization error: Plugin not configured");
                return "Plugin not configured";
            }

            //use only passed store identifier for the manual synchronization
            //use all store ids for the synchronization task
            var storeIds = manualSync ? new List<int> { storeScope }
                : new List<int> { 0 }.Union(_storeService.GetAllStores().Select(store => store.Id));

            foreach (var storeId in storeIds)
            {
                //get list identifier from the settings
                var listId = _settingService.GetSettingByKey<int>("SendInBlueSettings.ListId", storeId: storeId);
                if (listId > 0)
                {
                    //get notify url from the settings
                    var url = _settingService.GetSettingByKey<string>("SendInBlueSettings.UrlSync", storeId: storeId);
                    if (string.IsNullOrEmpty(url))
                        _logger.Warning("SendInBlue synchronization warning: Notify url not specified");

                    var subscriptions = _newsLetterSubscriptionService.GetAllNewsLetterSubscriptions(storeId: storeId, isActive: true);
                    if (subscriptions.Count == 0)
                    {
                        error = "There are no subscriptions";
                        continue;
                    }

                    //import subscriptions from nopCommerce to SendInBlue
                    var csv = subscriptions.Aggregate(string.Format("{0};{1}", "EMAIL", "STORE_ID"),
                        (current, next) => string.Format("{0}\n{1};{2}", current, next.Email, next.StoreId));

                    //sometimes occur Exception "Request failed" https://github.com/mailin-api/mailin-api-csharp/commit/d7d9f19fd6a18fee51ef7507e2020a972dc18093
                    //it does not affect the correct functioning
                    try
                    {
                        var importParams = new Dictionary<string, object>
                        {
                            { "notify_url", url },
                            { "body", csv },
                            { "listids", new List<int> { listId } }
                        };
                        var import = Manager.import_users(importParams);
                        if (!IsSuccess(import))
                        {
                            _logger.Error(string.Format("SendInBlue synchronization error: {0}", (string)import.message));
                            error = (string)import.message;
                        }
                    }
                    catch (Exception)
                    { }
                }
                else
                    error = "List ID is empty";
            }

            return error;
        }

        /// <summary>
        /// Delete unsubscribed user (in nopCommerce) from SendInBlue list
        /// </summary>
        /// <param name="email">Subscription email</param>
        public void Unsubscribe(string email)
        {
            if (!IsConfigured)
                _logger.Error("SendInBlue unsubscription error: Plugin not configured");

            //delete user from all lists
            var unsubscribeParams = new Dictionary<string, string> { { "email", email } };
            var unsubscribe = Manager.delete_user(unsubscribeParams);
            if (!IsSuccess(unsubscribe))
                _logger.Error(string.Format("SendInBlue unsubscription error: {0}", (string)unsubscribe.message));
        }

        /// <summary>
        /// Delete unsubscribed user (in SendInBlue) from nopCommerce subscription list
        /// </summary>
        /// <param name="unsubscriberUser">User information</param>
        public void UnsubscribeWebhook(string unsubscriberUser)
        {
            if (!IsConfigured)
                return;

            //parse string to JSON object
            dynamic unsubscriber = JObject.Parse(unsubscriberUser);

            //we pass the store identifier in the X-Mailin-Tag at sending emails, now get it here
            int storeId;
            if (!int.TryParse(unsubscriber.tag, out storeId))
                return;

            var store = _storeService.GetStoreById(storeId);
            if (store == null)
                return;

            //get subscription by email and store identifier
            var email = (string)unsubscriber.email;
            var subscription = _newsLetterSubscriptionService.GetNewsLetterSubscriptionByEmailAndStoreId(email, store.Id);
            if (subscription != null)
            {
                //delete subscription
                _newsLetterSubscriptionService.DeleteNewsLetterSubscription(subscription, false);
                _logger.Information(string.Format("SendInBlue unsubscription: email {0}, store {1}, date {2}", 
                    email, store.Name, (string)unsubscriber.date_event));
            }
        }

        /// <summary>
        /// Create webhook for the getting notification about unsubscribed users
        /// </summary>
        /// <param name="webhookId">Current webhook identifier</param>
        /// <param name="url">Url of the handler</param>
        /// <returns>Webhook id</returns>
        public int GetUnsubscribeWebHookId(int webhookId, string url)
        {
            if (!IsConfigured)
                return 0;

            //check that webhook already exist
            if (webhookId != 0)
            {
                var existWebhookParams = new Dictionary<string, int> { { "id", webhookId } };
                var existWebHook = Manager.get_webhook(existWebhookParams);
                if (IsSuccess(existWebHook))
                    return webhookId;
            }

            //or create new one
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

        /// <summary>
        /// Get SendInBlue common account information
        /// </summary>
        /// <param name="error">Errors</param>
        /// <returns>Account info</returns>
        public string GetAccountInfo(ref string error)
        {
            if (!IsConfigured)
                error = "Plugin not configured";
            else
            {
                var account = Manager.get_account();
                if (IsSuccess(account))
                    return string.Format("Name: {1}{0}Second name: {2}{0}Plan: {3}{0}Email credits: {4}{0}SMS credits: {5}{0}",
                        Environment.NewLine, HttpUtility.HtmlEncode(account.data[2].first_name), HttpUtility.HtmlEncode(account.data[2].last_name),
                        account.data[0].plan_type, account.data[0].credits, account.data[1].credits);
                else
                    error = (string)account.message;
            }

            return string.Empty;
        }

        /// <summary>
        /// Get available lists for the synchronization subscriptions
        /// </summary>
        /// <returns>List of lists</returns>
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

        /// <summary>
        /// Get available senders of transactional emails
        /// </summary>
        /// <returns>List of senders</returns>
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

        /// <summary>
        /// Check whether SMTP is enabled on SendInBlue profile
        /// </summary>
        /// <param name="error">Errors</param>
        /// <returns>True if status is enabled, otherwise false</returns>
        public bool SmtpIsEnabled(ref string error)
        {
            if (!IsConfigured)
                error = "Plugin not configured";
            else
            {
                var smtp = Manager.get_smtp_details();
                if (IsSuccess(smtp) && smtp.data.relay_data != null)
                    if (smtp.data.relay_data.status == "enabled")
                        return true;
                    else
                        error = string.Format("SMTP is {0}", smtp.data.relay_data.status);
                else
                    error = (string)smtp.message;
            }

            return false;
        }

        /// <summary>
        /// Create new list for synchronization subscriptions in SendInBlue account
        /// </summary>
        /// <param name="name">Name of the list</param>
        /// <returns>List identifier</returns>
        public int CreateNewList(string name)
        {
            if (!IsConfigured)
                return 0;

            //create all new lists in the particular nopCommerce folder
            //check that this folder already exists, otherwise to create it
            var allFoldersParams = new Dictionary<string, int> { { "page", 1 }, { "page_limit", 50 } };
            var allFolders = Manager.get_folders(allFoldersParams);
            if (!IsSuccess(allFolders))
                return 0;

            var folder = (allFolders.data.folders as JArray).FirstOrDefault(x => ((string)x["name"]).Contains("nopCommerce"));
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

            //create new list in the nopCommerce folder
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

        /// <summary>
        /// Create STORE_ID attribute in SendInBlue account
        /// </summary>
        public void PrepareStoreAttribute()
        {
            if (!IsConfigured)
                return;

            var attributeParams = new Dictionary<string, string> { { "type", "normal" } };
            var attribute = Manager.get_attribute(attributeParams);
            if (!IsSuccess(attribute))
                return;

            if ((attribute.data as JArray).Any(x => x["name"].ToString().Contains("STORE_ID")))
                return;
            
            var storeAttributeParams = new Dictionary<string, object>
            {
                { "type", "normal" },
                { "data", new Dictionary<string, string> { { "STORE_ID", "text" } } }
            };
            var storeAttribute = Manager.create_attribute(storeAttributeParams);
        }

        /// <summary>
        /// Get email account identifier
        /// </summary>
        /// <param name="emailAccountService">Email account service</param>
        /// <param name="senderId">Sender identifier</param>
        /// <param name="error">Errors</param>
        /// <returns>Email account identifier</returns>
        public int GetEmailAccountId(IEmailAccountService emailAccountService, string senderId, out string error)
        {
            error = string.Empty;
            if (!IsConfigured)
                return 0;

            //get all available senders
            var sendersParams = new Dictionary<string, string> { { "option", "" } };
            var senders = Manager.get_senders(sendersParams);
            if (!IsSuccess(senders))
            {
                error = (string)senders.message;
                return 0;
            }

            foreach (var sender in senders.data)
            {
                if (sender.id == senderId)
                {
                    //try to find existing email account by name and email
                    var emailAccount = emailAccountService.GetAllEmailAccounts().FirstOrDefault(x =>
                        x.DisplayName == (string)sender.from_name && x.Email == (string)sender.from_email);
                    if (emailAccount != null)
                        return emailAccount.Id;

                    //or create new one
                    var smtp = Manager.get_smtp_details();
                    if (!IsSuccess(smtp))
                    {
                        error = (string)senders.message;
                        return 0;
                    }

                    var newEmailAccount = new EmailAccount()
                    {
                        Host = smtp.data.relay_data.data.relay,
                        Port = smtp.data.relay_data.data.port,
                        Username = smtp.data.relay_data.data.username,
                        Password = smtp.data.relay_data.data.password,
                        EnableSsl = true,
                        Email = sender.from_email,
                        DisplayName = sender.from_name
                    };
                    emailAccountService.InsertEmailAccount(newEmailAccount);

                    return newEmailAccount.Id;
                }
            }

            return 0;
        }

        /// <summary>
        /// Synchronize nopCommerce tokens and SendInBlue transactional attributes
        /// </summary>
        /// <param name="tokens">Tokens</param>
        /// <param name="error">Errors</param>
        public void PrepareAttributes(IEnumerable<string> tokens, out string error)
        {
            error = string.Empty;
            if (!IsConfigured)
                return;

            //get already existing transactional attributes
            var attributesParams = new Dictionary<string, string> { { "type", "transactional" } };
            var attributes = Manager.get_attribute(attributesParams);
            if (!IsSuccess(attributes))
            {
                error = (string)attributes.message;
                return;
            }

            //bring tokens to SendInBlue attributes format
            tokens = tokens.Select(x => x.Replace("%", "").Replace(".", "_").Replace("(s)","-s-").ToUpperInvariant());

            //get attributes that are not already on SendInBlue account
            if (attributes.data[0] != null)
                tokens = tokens.Except((attributes.data[0] as JArray).Select(x => x["name"].ToString()).ToList());

            //and create their
            var notExistsAttributes = tokens.ToDictionary(x => x, x => "text");
            if (notExistsAttributes.Count > 0)
            {
                notExistsAttributes.Add("ID", "id");
                var newAttributeParams = new Dictionary<string, object> { { "type", "transactional" }, { "data", notExistsAttributes } };
                var newAttributes = Manager.create_attribute(newAttributeParams);
                if (!IsSuccess(newAttributes))
                    error = (string)newAttributes.message;
            }
        }

        /// <summary>
        /// Get SendInBlue email template identifier
        /// </summary>
        /// <param name="templateId">Current email template id</param>
        /// <param name="message">Message template</param>
        /// <param name="emailAccount">Email account</param>
        /// <returns>Email template identifier</returns>
        public int GetTemplateId(int templateId, MessageTemplate message, EmailAccount emailAccount)
        {
            if (!IsConfigured)
                return 0;

            //check that appropriate template already exist
            if (templateId > 0)
            {
                var templateParams = new Dictionary<string, int> { { "id", templateId } };
                var template = Manager.get_campaign_v2(templateParams);
                if (IsSuccess(template))
                    return templateId;
            }

            //or create new one
            if (emailAccount != null)
            {
                //the original body and subject of the email template are the same as that of the message template in nopCommerce
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

        /// <summary>
        /// Convert SendInBlue email template to queued email
        /// </summary>
        /// <param name="templateId">Email template identifier</param>
        /// <returns>Queued email</returns>
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

            //get template
            var templateParams = new Dictionary<string, int> { { "id", templateId } };
            var template = Manager.get_campaign_v2(templateParams);
            if (!IsSuccess(template))
            {
                _logger.Error(string.Format("SendInBlue email sending error: {0}", (string)template.message));
                return null;
            }

            //bring attributes to nopCommerce tokens format
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

        /// <summary>
        /// Send SMS 
        /// </summary>
        /// <param name="to">Phone number of the receiver</param>
        /// <param name="from">Name of sender</param>
        /// <param name="text">Text</param>
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
