using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Rendering;
using Newtonsoft.Json.Linq;
using Nop.Core.Domain.Messages;
using Nop.Services.Configuration;
using Nop.Services.Logging;
using Nop.Services.Messages;
using Nop.Services.Stores;
using SendInBlue.Client;
using SendInBlue.Api;
using Microsoft.AspNetCore.Mvc.Rendering;
using SendInBlue.Model;
using static SendInBlue.Model.CreateWebhook;
using static SendInBlue.Model.GetAttributesAttributes;
using Nop.Core.Domain.Orders;

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
        private bool IsConfigured => !string.IsNullOrEmpty(_settingService.LoadSetting<SendInBlueSettings>().ApiKey);

        /// <summary>
        /// Gets configuration for SendInBlue API
        /// </summary>
        private Configuration Config => new Configuration()
        {
            ApiKey = new Dictionary<string, string> { { "api-key", _settingService.LoadSetting<SendInBlueSettings>().ApiKey } }
        };

        /// <summary>
        /// Gets a collection of functions to interact with the API endpoints of Contacts 
        /// </summary>
        private ContactsApi ContactsApi => new ContactsApi(Config);

        /// <summary>
        /// Gets a collection of functions to interact with the API endpoints of Senders
        /// </summary>
        private SendersApi SendersApi => new SendersApi(Config);

        /// <summary>
        /// Gets a collection of functions to interact with the API endpoints of Account
        /// </summary>
        private AccountApi AccountApi => new AccountApi(Config);

        /// <summary>
        /// Gets a collection of functions to interact with the API endpoints of Webhook
        /// </summary>
        private WebhooksApi WebhooksApi => new WebhooksApi(Config);

        /// <summary>
        /// Gets a collection of functions to interact with the API endpoints of Attributes
        /// </summary>
        private AttributesApi AttributesApi => new AttributesApi(Config);

        /// <summary>
        /// Gets a collection of functions to interact with the API endpoints of Email
        /// </summary>
        private EmailCampaignsApi EmailCampaignsApi => new EmailCampaignsApi(Config);

        /// <summary>
        /// Gets a collection of functions to interact with the API endpoints of SMS
        /// </summary>
        private TransactionalSMSApi TransactionalSMSApi => new TransactionalSMSApi(Config);

        #endregion

        #region Methods

        /// <summary>
        /// Import subscriptions from nopCommerce to SendInBlue
        /// </summary>
        /// <param name="storeIds">Store IDs</param>
        /// <param name="error">Errors</param>
        private void ImportSubscriptions(IEnumerable<int> storeIds, out string error)
        {
            error = string.Empty;
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
                    var csv = subscriptions.Aggregate("EMAIL;STORE_ID",
                        (current, next) => $"{current}\n{next.Email};{next.StoreId}");

                    try
                    {
                        RequestContactImport requestContactImport = new RequestContactImport()
                        {
                            NotifyUrl = url,
                            FileBody = csv,
                            ListIds = new List<long?> { listId }
                        };
                        try
                        {
                            ContactsApi.ImportContacts(requestContactImport);
                        }
                        catch (ApiException e)
                        {
                            _logger.Error($"SendInBlue synchronization import contact error: {e.Message}");
                            error = "SendInBlue synchronization import contact error";
                        }
                    }
                    catch
                    {
                        // ignored
                    }
                }
                else
                    error = "List ID is empty";
            }
        }

        /// <summary>
        /// Export subscriptions from SendInBlue to nopCommerce
        /// </summary>
        /// <param name="storeIds">Store IDs</param>
        /// <param name="error">Errors</param>
        private void ExportSubscriptions(IEnumerable<int> storeIds, out string error)
        {
            error = string.Empty;
            foreach (var curStore in storeIds)
            {
                //get list identifier from the settings
                var listId = _settingService.GetSettingByKey<int>("SendInBlueSettings.ListId", storeId: curStore);
                if (listId > 0)
                {
                    try
                    {
                        //Checks if there is a contact in the list
                        var contacts = ContactsApi.GetContactsFromList(listId);
                        var list_contacts = JObject.Parse(contacts.ToJson())["contacts"];
                        var tokens = list_contacts.Select(x => x.SelectTokens("$")).ToList();

                        foreach (var contact in tokens)
                        {
                            foreach (var child in contact)
                            {
                                var storeId = int.Parse(child["attributes"]["STORE_ID"].ToString());
                                var subscription = _newsLetterSubscriptionService.GetNewsLetterSubscriptionByEmailAndStoreId(child["email"].ToString(), storeId);
                                subscription.Active = !bool.Parse(child["emailBlacklisted"].ToString().ToLower());
                                _newsLetterSubscriptionService.UpdateNewsLetterSubscription(subscription, false);
                            }
                        }
                    }
                    catch (ApiException e)
                    {
                        _logger.Error($"SendInBlue synchronization export contact error: {e.Message}");
                        error = "SendInBlue synchronization export contact error";
                    }
                }
                else
                    error = "List ID is empty";
            }
        }

        /// <summary>
        /// Synchronize subscriptions 
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

            ImportSubscriptions(storeIds, out error);
            ExportSubscriptions(storeIds, out error);

            return error;
        }

        #region Order Completed

        /// <summary>
        /// Completed order in nopCommerce
        /// </summary>
        /// <param name="order">Order</param>
        public void UpdateContact(Order order)
        {
            if (!IsConfigured)
                _logger.Error("SendInBlue subscription error: Plugin not configured");

            try
            {
                var attr = new Dictionary<string, object>
                {
                    { "ORDER_ID", order.Id.ToString()},
                    { "ORDER_DATE", order.PaidDateUtc.ToString() },
                    { "ORDER_PRICE", order.OrderTotal.ToString() }
                };

                var updateContact = new UpdateContact()
                {
                    Attributes = attr
                };
                ContactsApi.UpdateContact(order.Customer.Email, updateContact);
            }
            catch (ApiException e)
            {
                _logger.Error($"SendInBlue update contact order error: {e.Message}");
            }
        }

        #endregion

        #region Subscribe email

        /// <summary>
        /// Subscribed user (in nopCommerce) from SendInBlue list
        /// </summary>
        /// <param name="email">Subscription email</param>
        public void Subscribe(string email)
        {
            if (!IsConfigured)
                _logger.Error("SendInBlue subscription error: Plugin not configured");


            //Checks if there is a contact in the list
            var listId = _settingService.GetSettingByKey<int>("SendInBlueSettings.ListId", storeId: 0);
            var contacts = ContactsApi.GetContactsFromList(listId);
            var list_contacts = JObject.Parse(contacts.ToJson())["contacts"];
            var tokens = list_contacts.Select(x => x.SelectTokens("$")).ToList();

            var isContains = false;
            //TODO это нужно сделать на LINQ
            foreach (var contact in tokens)
            {
                foreach (var child in contact)
                    if (child["email"].ToString() == email.ToLower())
                    {
                        isContains = true;
                        break;
                    }
                if (isContains)
                    break;
            }

            //Add new contact
            if (!isContains)
            {
                //TODO не хватает StoreID для контакта
                try
                {
                    var createContact = new CreateContact()
                    {
                        Email = email,
                        Attributes = new Dictionary<string, string> { { "STORE_ID", null } },
                        ListIds = new List<long?> { listId },
                        UpdateEnabled = true
                    };
                    ContactsApi.CreateContact(createContact);
                }
                catch (ApiException e)
                {
                    _logger.Error($"SendInBlue create contact error: {e.Message}");
                }
            }
            else
            {
                //move contact into blacklist
                try
                {
                    var updateContact = new UpdateContact()
                    {
                        EmailBlacklisted = false
                    };
                    ContactsApi.UpdateContact(email, updateContact);
                }
                catch (ApiException e)
                {
                    _logger.Error($"SendInBlue subscription error: {e.Message}");
                }
            }
        }

        #endregion

        #region Unsubscribe email

        /// <summary>
        /// Unsubscribed user (in nopCommerce) from SendInBlue list
        /// </summary>
        /// <param name="email">Subscription email</param>
        public void Unsubscribe(string email)
        {
            if (!IsConfigured)
                _logger.Error("SendInBlue unsubscription error: Plugin not configured");

            //move user to blacklist
            try
            {
                var updateContact = new UpdateContact()
                {
                    EmailBlacklisted = true
                };
                ContactsApi.UpdateContact(email, updateContact);
            }
            catch (ApiException e)
            {
                _logger.Error($"SendInBlue unsubscription error: {e.Message}");
            }
        }

        /// <summary>
        /// Unsubscribed user (in SendInBlue) from nopCommerce subscription list
        /// </summary>
        /// <param name="unsubscriberUser">User information</param>
        public void UnsubscribeWebhook(string unsubscriberUser)
        {
            if (!IsConfigured)
                return;

            //parse string to JSON object
            dynamic unsubscriber = JObject.Parse(unsubscriberUser);

            //we pass the store identifier in the X-Mailin-Tag at sending emails, now get it here
            if (!int.TryParse(unsubscriber.tag, out int storeId))
                return;

            var store = _storeService.GetStoreById(storeId);
            if (store == null)
                return;

            //get subscription by email and store identifier
            var email = (string)unsubscriber.email;
            var subscription = _newsLetterSubscriptionService.GetNewsLetterSubscriptionByEmailAndStoreId(email, store.Id);
            if (subscription == null)
                return;

            //update subscription
            _newsLetterSubscriptionService.UpdateNewsLetterSubscription(subscription);
            _logger.Information($"SendInBlue unsubscription: email {email}, store {store.Name}, date {(string)unsubscriber.date_event}");
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
                try
                {
                    GetWebhook result = WebhooksApi.GetWebhook(webhookId);
                    return webhookId;
                }
                catch (ApiException e)
                {
                    _logger.Error($"Exception when calling WebhooksApi#getWebhook: {e.Message}");
                }
            }

            //or create new one
            var webhookParams = new CreateWebhook(url, null, new List<EventsEnum> { EventsEnum.Unsubscribed }, CreateWebhook.TypeEnum.Transactional);

            try
            {
                CreateModel result = WebhooksApi.CreateWebhook(webhookParams);
                return (int)result.Id;
            }
            catch (ApiException e)
            {
                _logger.Error($"Exception when calling WebhooksApi#CreateWebhook: {e.Message}");
            }

            return 0;
        }

        #endregion

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
                try
                {
                    GetAccount account = AccountApi.GetAccount();
                    return string.Format("Name: {1}{0}Second name: {2}{0}Email: {3}{0}Email credits: {4}{0}SMS credits: {5}{0}",
                        Environment.NewLine, WebUtility.HtmlEncode(account.FirstName ?? string.Empty), WebUtility.HtmlEncode(account.LastName ?? string.Empty),
                        WebUtility.HtmlEncode(account.Email ?? string.Empty),
                        account.Plan.FirstOrDefault<GetAccountPlan>(x => x.Type == GetAccountPlan.TypeEnum.Free).Credits.ToString(),
                        account.Plan.FirstOrDefault<GetAccountPlan>(x => x.Type == GetAccountPlan.TypeEnum.Sms).Credits.ToString());
                }
                catch (ApiException e)
                {
                    error = e.Message;
                    _logger.Error($"Exception when calling AccountApi#GetAccount: {e.Message}");
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// Set partner of account
        /// </summary>
        /// <returns>True if partner successfully set; otherwise false</returns>
        public bool SetPartner()
        {
            if (!IsConfigured)
                return false;

            try
            {
                AccountApi.SetPartner(new SetPartner(SendInBlueDefaults.PartnerName));
            }
            catch (ApiException e)
            {
                _logger.Error($"Exception when calling AccountApi#SetPartner: {e.Message}");
                return false;
            }

            return true;
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

            long limit = 50;
            long offset = 1;

            try
            {
                GetLists lists = ContactsApi.GetLists(limit, offset);

                var lists_obj = JObject.Parse(lists.ToJson())["lists"];
                var tokens = lists_obj.Select(x => x.SelectTokens("$")).ToList();

                //TODO это нужно сделать на LINQ
                foreach (var token in tokens)
                    foreach (var child in token)
                        availableLists.Add(new SelectListItem { Text = child["name"].ToString(), Value = child["id"].ToString() });
            }
            catch (ApiException e)
            {
                _logger.Error($"Exception when calling ContactsApi#getLists: {e.Message}");
            }

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

            string ip = string.Empty;
            string domain = string.Empty;

            try
            {
                var senders = SendersApi.GetSenders(ip, domain);
                foreach (var sender in senders.Senders)
                    availableSenders.Add(new SelectListItem
                    {
                        Text = string.Format("{0} ({1})", sender.Name, sender.Email),
                        Value = sender.Id.ToString()
                    });
            }
            catch (ApiException e)
            {
                _logger.Error($"Exception when calling SendersApi#getSenders: {e.Message}");
            }

            /*var sendersParams = new Dictionary<string, string> { { "option", "" } };
            var senders = Manager.get_senders(sendersParams);
            if (!IsSuccess(senders))
                return availableSenders;

            foreach (var sender in senders.data)
                availableSenders.Add(new SelectListItem
                {
                    Text = string.Format("{0} ({1})", sender.from_name, sender.from_email),
                    Value = sender.id
                });
                */
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
                try
                {
                    var res = AccountApi.GetAccount().Relay.Enabled ?? false;
                    if (res)
                        return true;
                    else
                        error = string.Format("SMTP is disabled");
                }
                catch (ApiException e)
                {
                    _logger.Error($"Exception when calling SendersApi#getSenders: {e.Message}");
                    error = e.Message;
                }
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

            long limit = 50;
            long offset = 1;

            try
            {
                GetFolders allFolders = ContactsApi.GetFolders(limit, offset);
                if (allFolders.Count == 0)
                    return 0;
                var folder = allFolders.Folders.FirstOrDefault(x => (x.ToString()).Contains("nopCommerce"));
                int folderId;
                if (folder == null)
                {
                    CreateUpdateFolder createFolder = new CreateUpdateFolder
                    {
                        Name = "nopCommerce"
                    };
                    var newFolder = ContactsApi.CreateFolder(createFolder);
                    if (newFolder != null)
                        return 0;

                    folderId = (int)newFolder.Id;
                }
                else
                {
                    folderId = (int)((JObject)folder).GetValue("id");
                }
                try
                {
                    //create new list in the nopCommerce folder
                    CreateList createList = new CreateList(name, folderId);
                    var newList = ContactsApi.CreateList(createList);

                    return (int)newList.Id;
                }
                catch (ApiException e)
                {
                    _logger.Error($"Exception when calling ContactsApi#CreateList: {e.Message}");
                }
            }
            catch (ApiException e)
            {
                _logger.Error($"Exception when calling FoldersApi#getFolders: {e.Message}");
            }

            return 0;
            /*
            var allFoldersParams = new Dictionary<string, int> { { "page", 1 }, { "page_limit", 50 } };
            var allFolders = Manager.get_folders(allFoldersParams);
            if (!IsSuccess(allFolders))
                return 0;

            var folder = (allFolders.data.folders as JArray)?.FirstOrDefault(x => ((string)x["name"]).Contains("nopCommerce"));
            int folderId;
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
            */
        }

        /// <summary>
        /// Add new attribute in SendinBlue Contact Attributes
        /// </summary>
        /// <param name="attrCategory">Category of attribute</param>
        /// <param name="attributes">Collection attributes for insert</param>
        private void AddAttibutes(CategoryEnum attrCategory, Dictionary<string, string> attributes)
        {
            foreach (var attr in attributes)
            {
                try
                {
                    if (attrCategory.ToString().ToLower() == "normal" || attrCategory.ToString().ToLower() == "category" || attrCategory.ToString().ToLower() == "transactional")
                        AttributesApi.CreateAttribute(attrCategory.ToString().ToLower(), attr.Key, new CreateAttribute(attr.Value, null, CreateAttribute.TypeEnum.Text));
                    else
                        AttributesApi.CreateAttribute(attrCategory.ToString().ToLower(), attr.Key, new CreateAttribute(attr.Value, null, null));
                }
                catch (ApiException e)
                {
                    _logger.Error($"Exception when calling AttributesApi#createAttribute: {e.Message}");
                }
            }
        }

        /// <summary>
        /// Create attributes in SendInBlue account
        /// </summary>
        public void CreateAttributes()
        {
            if (!IsConfigured)
                return;

            try
            {
                var attributes = AttributesApi.GetAttributes();

                // Create STORE_ID attribute in SendInBlue account
                if (!attributes.Attributes.Any(x => x.Name.Contains("STORE_ID")))
                {
                    Dictionary<string, string> newAttrs = new Dictionary<string, string> { { "STORE_ID", null } };
                    AddAttibutes(CategoryEnum.Normal, newAttrs);
                }

                // Create transactional attributes in SendInBlue account
                if (!(attributes.Attributes.Any(x => x.Name.Contains("ORDER_ID")) &&
                    attributes.Attributes.Any(x => x.Name.Contains("ORDER_DATE")) &&
                    attributes.Attributes.Any(x => x.Name.Contains("ORDER_PRICE"))))
                {
                    Dictionary<string, string> newAttrs = new Dictionary<string, string>
                    {
                        { "ORDER_ID", null }, { "ORDER_DATE", null }, { "ORDER_PRICE", null }
                    };

                    AddAttibutes(CategoryEnum.Transactional, newAttrs);
                }

                // Create calculated attributes in SendInBlue account
                if (!(attributes.Attributes.Any(x => x.Name.Contains("NOPCOMMERCE_CA_USER")) &&
                    attributes.Attributes.Any(x => x.Name.Contains("NOPCOMMERCE_LAST_30_DAYS_CA")) &&
                    attributes.Attributes.Any(x => x.Name.Contains("NOPCOMMERCE_ORDER_TOTAL"))))
                {
                    Dictionary<string, string> newAttrs = new Dictionary<string, string>
                    {
                        { "NOPCOMMERCE_CA_USER", "SUM[ORDER_PRICE]" },
                        { "NOPCOMMERCE_LAST_30_DAYS_CA", "SUM[ORDER_PRICE,ORDER_DATE,>,NOW(-30)]" },
                        { "NOPCOMMERCE_ORDER_TOTAL", "COUNT[ORDER_ID]" }
                    };

                    AddAttibutes(CategoryEnum.Calculated, newAttrs);
                }

                // Create global attributes in SendInBlue account
                if (!(attributes.Attributes.Any(x => x.Name.Contains("NOPCOMMERCE_CA_TOTAL")) &&
                    attributes.Attributes.Any(x => x.Name.Contains("NOPCOMMERCE_CA_LAST_30DAYS")) &&
                    attributes.Attributes.Any(x => x.Name.Contains("NOPCOMMERCE_ORDERS_COUNT"))))
                {
                    Dictionary<string, string> newAttrs = new Dictionary<string, string>
                    {
                        { "NOPCOMMERCE_CA_TOTAL", "SUM[NOPCOMMERCE_CA_USER]" },
                        { "NOPCOMMERCE_CA_LAST_30DAYS", "SUM[NOPCOMMERCE_LAST_30_DAYS_CA]" },
                        { "NOPCOMMERCE_ORDERS_COUNT", "SUM[NOPCOMMERCE_ORDER_TOTAL]" }
                    };

                    AddAttibutes(CategoryEnum.Global, newAttrs);
                }
            }
            catch (ApiException e)
            {
                _logger.Error($"Exception when calling AttributesApi#getAttributes: {e.Message}");
            }

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

            try
            {
                //get all available senders
                var senders = SendersApi.GetSenders();
                if (senders.Senders.Count == 0)
                {
                    error = "No senders";
                    return 0;
                }
                foreach (var sender in senders.Senders)
                {
                    if (sender.Id.ToString() != senderId)
                        continue;

                    //try to find existing email account by name and email
                    var emailAccount = emailAccountService.GetAllEmailAccounts().FirstOrDefault(x =>
                       x.DisplayName == sender.Name && x.Email == sender.Email);
                    if (emailAccount != null)
                        return emailAccount.Id;

                    var relay = AccountApi.GetAccount().Relay;

                    //or create new one 
                    var newEmailAccount = new EmailAccount
                    {
                        Host = relay.Data.Relay,
                        Port = (int)relay.Data.Port,
                        Username = relay.Data.UserName,
                        Password = _settingService.LoadSetting<SendInBlueSettings>().SMTPPassword,
                        EnableSsl = true,
                        Email = sender.Email,
                        DisplayName = sender.Name
                    };
                    emailAccountService.InsertEmailAccount(newEmailAccount);

                    return newEmailAccount.Id;
                }
            }
            catch (ApiException e)
            {
                _logger.Error($"Exception when calling SendersApi#getSenders: {e.Message}");
            }

            return 0;

            /*//get all available senders
            var sendersParams = new Dictionary<string, string> { { "option", "" } };
            var senders = Manager.get_senders(sendersParams);
            if (!IsSuccess(senders))
            {
                error = (string)senders.message;
                return 0;
            }

            foreach (var sender in senders.data)
            {
                if (sender.id != senderId) 
                    continue;

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

                var newEmailAccount = new EmailAccount
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

            return 0;*/
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

            try
            {
                //get already existing transactional attributes
                var attributes = AttributesApi.GetAttributes();
                if (attributes.Attributes.Count(x => x.Category == CategoryEnum.Transactional) == 0)
                {
                    error = "No attributes";
                    return;
                }

                //bring tokens to SendInBlue attributes format
                tokens = tokens.Select(x => x.Replace("%", "").Replace(".", "_").Replace("(s)", "-s-").ToUpperInvariant());

                //get attributes that are not already on SendInBlue account 
                if (attributes.Attributes.Count(x => x.Category == CategoryEnum.Transactional) == 0)
                    tokens = tokens.Except((attributes.Attributes.Select(x => x.Category = CategoryEnum.Transactional) as JArray)?.Select(x => x["name"].ToString()).ToList() ?? new List<string>());

                //and create their
                var notExistsAttributes = tokens.ToDictionary(x => x, x => "text");
                if (notExistsAttributes.Count <= 0)
                    return;

                notExistsAttributes.Add("ID", "id");

                AddAttibutes(CategoryEnum.Transactional, notExistsAttributes);
            }
            catch (ApiException e)
            {
                _logger.Error($"Exception when calling AttributesApi#getAttributes: {e.Message}");
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
                try
                {
                    EmailCampaignsApi.GetEmailCampaign(templateId);
                    return templateId;
                }
                catch (ApiException e)
                {
                    _logger.Error($"Exception when calling EmailCampaignsApi#getEmailCampaign: {e.Message}");
                }
            }

            //or create new one
            if (emailAccount == null)
                return 0;

            //the original body and subject of the email template are the same as that of the message template in nopCommerce
            var body = Regex.Replace(message.Body, "(%[^\\%]*.%)", x => x.ToString().Replace(".", "_").Replace("(s)", "-s-").ToUpperInvariant());
            var subject = Regex.Replace(message.Subject, "(%[^\\%]*.%)", x => x.ToString().Replace(".", "_").Replace("(s)", "-s-").ToUpperInvariant());

            CreateEmailCampaign emailCampaigns = new CreateEmailCampaign()
            {
                Name = message.Name,
                HtmlContent = body,
                Subject = subject,
                Sender = new CreateEmailCampaignSender(emailAccount.DisplayName, emailAccount.Email),
                Type = CreateEmailCampaign.TypeEnum.Classic
            };

            try
            {
                var newTemplate = EmailCampaignsApi.CreateEmailCampaign(emailCampaigns);
                return (int)newTemplate.Id;
            }
            catch (ApiException e)
            {
                _logger.Error($"Exception when calling EmailCampaignsApi#createEmailCampaign: {e.Message}");
            }

            return 0;

            /*
            //check that appropriate template already exist
            if (templateId > 0)
            {
                var templateParams = new Dictionary<string, int> { { "id", templateId } };
                var template = Manager.get_campaign_v2(templateParams);
                if (IsSuccess(template))
                    return templateId;
            }

            //or create new one
            if (emailAccount == null) 
                return 0;

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

            return 0;*/
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

            try
            {
                //get template
                var template = EmailCampaignsApi.GetEmailCampaign(templateId);

                //bring attributes to nopCommerce tokens format
                var subject = Regex.Replace(template.Subject, "(%[^_]*_.*%)", x => x.ToString().Replace("_", ".").Replace("-S-", "(s)"));
                var body = Regex.Replace(template.HtmlContent, "(%[^_]*_.*%)", x => x.ToString().Replace("_", ".").Replace("-S-", "(s)"));

                return new QueuedEmail
                {
                    Subject = subject,
                    Body = body,
                    FromName = template.Sender.Name,
                    From = template.Sender.Email
                };
            }
            catch (ApiException e)
            {
                _logger.Error($"Exception when calling EmailCampaignsApi#getEmailCampaign: {e.Message}");
            }

            _logger.Error($"SendInBlue email sending error");
            return null;
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

            SendTransacSms sendTransacSms = new SendTransacSms()
            {
                Type = SendTransacSms.TypeEnum.Transactional,
                Sender = from,
                Recipient = to,
                Content = text
            };

            try
            {
                var sms = TransactionalSMSApi.SendTransacSms(sendTransacSms);
                _logger.Information(
                    $"SendInBlue SMS sent: {sms.Reference ?? $"credits remaining {sms.RemainingCredits.ToString()}"}");
            }
            catch (ApiException e)
            {
                _logger.Error($"Exception when calling TransactionalSMSApi#sendTransacSms: {e.Message}");
            }
        }

        #endregion
    }
}