using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.Routing;
using Newtonsoft.Json;
using Nop.Core;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Messages;
using Nop.Core.Domain.Orders;
using Nop.Services.Common;
using Nop.Services.Configuration;
using Nop.Services.Customers;
using Nop.Services.Directory;
using Nop.Services.Logging;
using Nop.Services.Messages;
using Nop.Services.Stores;
using Nop.Web.Framework.UI;
using SendInBlue.Api;
using SendInBlue.Client;
using SendInBlue.Model;
using static SendInBlue.Model.GetAttributesAttributes;

namespace Nop.Plugin.Misc.SendInBlue.Services
{
    /// <summary>
    /// Represents SendInBlue manager
    /// </summary>
    public class SendInBlueManager
    {
        #region Fields

        private readonly IActionContextAccessor _actionContextAccessor;
        private readonly ICountryService _countryService;
        private readonly ICustomerService _customerService;
        private readonly IEmailAccountService _emailAccountService;
        private readonly IGenericAttributeService _genericAttributeService;
        private readonly ILogger _logger;
        private readonly INewsLetterSubscriptionService _newsLetterSubscriptionService;
        private readonly ISettingService _settingService;
        private readonly IStoreService _storeService;
        private readonly IUrlHelperFactory _urlHelperFactory;
        private readonly IWebHelper _webHelper;
        private readonly IWorkContext _workContext;

        #endregion

        #region Ctor

        public SendInBlueManager(IActionContextAccessor actionContextAccessor,
            ICountryService countryService,
            ICustomerService customerService,
            IEmailAccountService emailAccountService,
            IGenericAttributeService genericAttributeService,
            ILogger logger,
            INewsLetterSubscriptionService newsLetterSubscriptionService,
            ISettingService settingService,
            IStoreService storeService,
            IUrlHelperFactory urlHelperFactory,
            IWebHelper webHelper,
            IWorkContext workContext)
        {
            this._actionContextAccessor = actionContextAccessor;
            this._countryService = countryService;
            this._customerService = customerService;
            this._emailAccountService = emailAccountService;
            this._genericAttributeService = genericAttributeService;
            this._logger = logger;
            this._newsLetterSubscriptionService = newsLetterSubscriptionService;
            this._settingService = settingService;
            this._storeService = storeService;
            this._urlHelperFactory = urlHelperFactory;
            this._webHelper = webHelper;
            this._workContext = workContext;
        }

        #endregion

        #region Utilities

        /// <summary>
        /// Prepare API client
        /// </summary>
        /// <returns>API client</returns>
        private TClient CreateApiClient<TClient>(Func<Configuration, TClient> clientCtor) where TClient : IApiAccessor
        {
            //check whether plugin is configured to request services (validate API key)
            var sendInBlueSettings = _settingService.LoadSetting<SendInBlueSettings>();
            if (string.IsNullOrEmpty(sendInBlueSettings.ApiKey))
                throw new NopException($"Plugin not configured");

            var apiConfiguration = new Configuration()
            {
                ApiKey = new Dictionary<string, string> { [SendInBlueDefaults.ApiKeyHeader] = sendInBlueSettings.ApiKey },
                UserAgent = SendInBlueDefaults.UserAgent
            };

            return clientCtor(apiConfiguration);
        }

        /// <summary>
        /// Import contacts from passed stores to account
        /// </summary>
        /// <param name="storeIds">List of store identifiers</param>
        /// <returns>List of messages</returns>
        private IList<(NotifyType Type, string Message)> ImportContacts(IList<int> storeIds)
        {
            var messages = new List<(NotifyType, string)>();

            //import contacts to account
            try
            {
                //create API client
                var client = CreateApiClient(config => new ContactsApi(config));

                foreach (var storeId in storeIds)
                {
                    //get list identifier from the settings
                    var listId = _settingService.GetSettingByKey<int>("SendInBlueSettings.ListId", storeId: storeId);
                    if (listId == 0)
                    {
                        _logger.Warning($"SendInBlue synchronization warning: List ID is empty for store #{storeId}");
                        messages.Add((NotifyType.Warning, $"List ID is empty for store #{storeId}"));
                        continue;
                    }

                    //try to get store subscriptions
                    var subscriptions = _newsLetterSubscriptionService.GetAllNewsLetterSubscriptions(storeId: storeId, isActive: true);
                    if (!subscriptions.Any())
                    {
                        _logger.Warning($"SendInBlue synchronization warning: There are no subscriptions for store #{storeId}");
                        messages.Add((NotifyType.Warning, $"There are no subscriptions for store #{storeId}"));
                        continue;
                    }

                    //get notification URL
                    var urlHelper = _urlHelperFactory.GetUrlHelper(_actionContextAccessor.ActionContext);
                    var notificationUrl = urlHelper.RouteUrl(SendInBlueDefaults.ImportContactsRoute, null, _webHelper.CurrentRequestProtocol);

                    //prepare CSV 
                    var title =
                        $"{SendInBlueDefaults.EmailServiceAttribute};" +
                        $"{SendInBlueDefaults.FirstNameServiceAttribute};" +
                        $"{SendInBlueDefaults.LastNameServiceAttribute};" +
                        $"{SendInBlueDefaults.UsernameServiceAttribute};" +
                        $"{SendInBlueDefaults.SMSServiceAttribute};" +
                        $"{SendInBlueDefaults.PhoneServiceAttribute};" +
                        $"{SendInBlueDefaults.CountryServiceAttribute};" +
                        $"{SendInBlueDefaults.StoreIdServiceAttribute}";
                    var csv = subscriptions.Aggregate(title, (all, subscription) =>
                    {
                        var firstName = string.Empty;
                        var lastName = string.Empty;
                        var phone = string.Empty;
                        var countryName = string.Empty;
                        var SMS = string.Empty;

                        var customer = _customerService.GetCustomerByEmail(subscription.Email);
                        if (customer != null)
                        {
                            firstName = _genericAttributeService.GetAttribute<string>(customer, NopCustomerDefaults.FirstNameAttribute);
                            lastName = _genericAttributeService.GetAttribute<string>(customer, NopCustomerDefaults.LastNameAttribute);
                            phone = _genericAttributeService.GetAttribute<string>(customer, NopCustomerDefaults.PhoneAttribute);
                            var countryId = _genericAttributeService.GetAttribute<int>(customer, NopCustomerDefaults.CountryIdAttribute);
                            var country = _countryService.GetCountryById(countryId);
                            countryName = country?.Name;
                            var countryISOCode = country?.NumericIsoCode ?? 0;
                            if (countryISOCode > 0)
                                SMS = phone.Replace("+" + ISO3166.FromISOCode(countryISOCode).DialCodes.FirstOrDefault().Replace(" ", ""), "");
                        }
                        return $"{all}\n" +
                            $"{subscription.Email};" +
                            $"{firstName};" +
                            $"{lastName};" +
                            $"{customer?.Username};" +
                            $"{SMS};" +
                            $"{phone};" +
                            $"{countryName};" +
                            $"{subscription.StoreId}";
                    });
                    
                    //prepare data to import
                    var requestContactImport = new RequestContactImport
                    {
                        NotifyUrl = notificationUrl,
                        FileBody = csv,
                        ListIds = new List<long?> { listId }
                    };

                    //start import
                    client.ImportContacts(requestContactImport);
                }
            }
            catch (Exception exception)
            {
                //log full error
                _logger.Error($"SendInBlue synchronization error: {exception.Message}", exception, _workContext.CurrentCustomer);
                messages.Add((NotifyType.Error, $"SendInBlue synchronization error: {exception.Message}"));
            }

            return messages;
        }

        /// <summary>
        /// Export contacts from account to passed stores
        /// </summary>
        /// <param name="storeIds">List of store identifiers</param>
        /// <returns>List of messages</returns>
        private IList<(NotifyType Type, string Message)> ExportContacts(IList<int> storeIds)
        {
            var messages = new List<(NotifyType, string)>();

            try
            {
                //create API client
                var client = CreateApiClient(config => new ContactsApi(config));

                foreach (var storeId in storeIds)
                {
                    //get list identifier from the settings
                    var listId = _settingService.GetSettingByKey<int>("SendInBlueSettings.ListId", storeId: storeId, loadSharedValueIfNotFound: true);
                    if (listId == 0)
                    {
                        _logger.Warning($"SendInBlue synchronization warning: List ID is empty for store #{storeId}");
                        messages.Add((NotifyType.Warning, $"List ID is empty for store #{storeId}"));
                        continue;
                    }

                    //check whether there are contacts in the list
                    var contacts = client.GetContactsFromList(listId);
                    var template = new { contacts = new[] { new { email = string.Empty, emailBlacklisted = false } } };
                    var contactObjects = JsonConvert.DeserializeAnonymousType(contacts.ToJson(), template);
                    var blackListedEmails = contactObjects?.contacts?.Where(contact => contact.emailBlacklisted)
                        .Select(contact => contact.email).ToList() ?? new List<string>();

                    foreach (var email in blackListedEmails)
                    {
                        //email in black list, so unsubscribe contact from all stores
                        foreach (var id in _storeService.GetAllStores().Select(store => store.Id))
                        {
                            var subscription = _newsLetterSubscriptionService.GetNewsLetterSubscriptionByEmailAndStoreId(email, id);
                            if (subscription != null)
                            {
                                subscription.Active = false;
                                _newsLetterSubscriptionService.UpdateNewsLetterSubscription(subscription, false);
                            }
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                //log full error
                _logger.Error($"SendInBlue synchronization error: {exception.Message}", exception, _workContext.CurrentCustomer);
                messages.Add((NotifyType.Error, $"SendInBlue synchronization error: {exception.Message}"));
            }

            return messages;
        }

        /// <summary>
        /// Add new service attribute in account
        /// </summary>
        /// <param name="category">Category of attribute</param>
        /// <param name="attributes">Collection of attributes</param>
        /// <returns>Errors if exist</returns>
        private string CreateAttibutes(IList<(CategoryEnum Category, string Name, string Value, CreateAttribute.TypeEnum? Type)> attributes)
        {
            if (!attributes.Any())
                return string.Empty;

            try
            {
                //create API client
                var client = CreateApiClient(config => new AttributesApi(config));

                foreach (var attribute in attributes)
                {
                    //prepare data
                    var createAttribute = new CreateAttribute(attribute.Value, type: attribute.Type);

                    //create attribute
                    client.CreateAttribute(attribute.Category.ToString().ToLowerInvariant(), attribute.Name, createAttribute);
                }
            }
            catch (Exception exception)
            {
                //log full error
                _logger.Error($"SendInBlue error: {exception.Message}.", exception, _workContext.CurrentCustomer);
                return exception.Message;
            }

            return string.Empty;
        }

        #endregion

        #region Methods

        #region Synchronization

        /// <summary>
        /// Synchronize contacts 
        /// </summary>
        /// <param name="synchronizationTask">Whether it's a scheduled synchronization</param>
        /// <param name="storeId">Store identifier; pass 0 to synchronize contacts for all stores</param>
        /// <returns>List of messages</returns>
        public IList<(NotifyType Type, string Message)> Synchronize(bool synchronizationTask = true, int storeId = 0)
        {
            var messages = new List<(NotifyType, string)>();
            try
            {
                //whether plugin is configured
                var sendInBlueSettings = _settingService.LoadSetting<SendInBlueSettings>();
                if (string.IsNullOrEmpty(sendInBlueSettings.ApiKey))
                    throw new NopException($"Plugin not configured");

                //use only passed store identifier for the manual synchronization
                //use all store ids for the synchronization task
                var storeIds = !synchronizationTask
                    ? new List<int> { storeId }
                    : new List<int> { 0 }.Union(_storeService.GetAllStores().Select(store => store.Id)).ToList();

                var importMessages = ImportContacts(storeIds);
                messages.AddRange(importMessages);

                var exportMessages = ExportContacts(storeIds);
                messages.AddRange(exportMessages);

            }
            catch (Exception exception)
            {
                //log full error
                _logger.Error($"SendInBlue synchronization error: {exception.Message}.", exception, _workContext.CurrentCustomer);
                messages.Add((NotifyType.Error, $"SendInBlue synchronization error: {exception.Message}"));
            }

            return messages;
        }

        /// <summary>
        /// Subscribe new contact
        /// </summary>
        /// <param name="subscription">Subscription</param>
        public void Subscribe(NewsLetterSubscription subscription)
        {
            try
            {
                //create API client
                var client = CreateApiClient(config => new ContactsApi(config));

                //try to get list identifier
                var listId = _settingService.GetSettingByKey<int>("SendInBlueSettings.ListId", storeId: subscription.StoreId);
                if (listId == 0)
                    listId = _settingService.GetSettingByKey<int>("SendInBlueSettings.ListId");
                if (listId == 0)
                {
                    _logger.Warning($"SendInBlue synchronization warning: List ID is empty for store #{subscription.StoreId}");
                    return;
                }

                //get all contacts of the list
                var contacts = client.GetContactsFromList(listId);

                //whether subscribed contact already in the list
                var template = new { contacts = new[] { new { email = string.Empty } } };
                var contactObjects = JsonConvert.DeserializeAnonymousType(contacts.ToJson(), template);
                var alreadyExist = contactObjects?.contacts?.Any(contact => contact.email == subscription.Email.ToLower()) ?? false;

                //Add new contact
                if (!alreadyExist)
                {
                    var firstName = string.Empty;
                    var lastName = string.Empty;
                    var phone = string.Empty;
                    var SMS = string.Empty;
                    var countryName = string.Empty;
                    var customer = _customerService.GetCustomerByEmail(subscription.Email);
                    if (customer != null)
                    {
                        firstName = _genericAttributeService.GetAttribute<string>(customer, NopCustomerDefaults.FirstNameAttribute);
                        lastName = _genericAttributeService.GetAttribute<string>(customer, NopCustomerDefaults.LastNameAttribute);
                        phone = _genericAttributeService.GetAttribute<string>(customer, NopCustomerDefaults.PhoneAttribute);
                        var countryId = _genericAttributeService.GetAttribute<int>(customer, NopCustomerDefaults.CountryIdAttribute);
                        var country = _countryService.GetCountryById(countryId);
                        countryName = country?.Name;

                        var countryISOCode = country?.NumericIsoCode ?? 0;
                        if (countryISOCode > 0)
                            SMS = phone.Replace("+" + ISO3166.FromISOCode(countryISOCode).DialCodes.FirstOrDefault().Replace(" ", ""), "");

                    }
                    var attributes = new Dictionary<string, string>
                    {
                        [SendInBlueDefaults.FirstNameServiceAttribute] = firstName,
                        [SendInBlueDefaults.LastNameServiceAttribute] = lastName,
                        [SendInBlueDefaults.UsernameServiceAttribute] = customer?.Username,
                        [SendInBlueDefaults.SMSServiceAttribute] = SMS,
                        [SendInBlueDefaults.PhoneServiceAttribute] = phone,
                        [SendInBlueDefaults.CountryServiceAttribute] = countryName,
                        [SendInBlueDefaults.StoreIdServiceAttribute] = subscription.StoreId.ToString()
                    };
                    var createContact = new CreateContact
                    {
                        Email = subscription.Email,
                        Attributes = attributes,
                        ListIds = new List<long?> { listId },
                        UpdateEnabled = true
                    };
                    client.CreateContact(createContact);
                }
                else
                {
                    //update contact
                    var updateContact = new UpdateContact
                    {
                        ListIds = new List<long?> { listId },
                        EmailBlacklisted = false
                    };
                    client.UpdateContact(subscription.Email, updateContact);
                }
            }
            catch (Exception exception)
            {
                //log full error
                _logger.Error($"SendInBlue error: {exception.Message}.", exception, _workContext.CurrentCustomer);
            }
        }

        /// <summary>
        /// Unsubscribe contact
        /// </summary>
        /// <param name="subscription">Subscription</param>
        public void Unsubscribe(NewsLetterSubscription subscription)
        {
            try
            {
                //create API client
                var client = CreateApiClient(config => new ContactsApi(config));

                //try to get list identifier
                var listId = _settingService.GetSettingByKey<int>("SendInBlueSettings.ListId", storeId: subscription.StoreId);
                if (listId == 0)
                    listId = _settingService.GetSettingByKey<int>("SendInBlueSettings.ListId");
                if (listId == 0)
                {
                    _logger.Warning($"SendInBlue synchronization warning: List ID is empty for store #{subscription.StoreId}");
                    return;
                }

                //update contact
                var updateContact = new UpdateContact
                {
                    UnlinkListIds = new List<long?> { listId }
                };
                client.UpdateContact(subscription.Email, updateContact);
            }
            catch (Exception exception)
            {
                //log full error
                _logger.Error($"SendInBlue error: {exception.Message}.", exception, _workContext.CurrentCustomer);
            }
        }

        /// <summary>
        /// Unsubscribe contact
        /// </summary>
        /// <param name="unsubscribeContact">Contact information</param>
        public void UnsubscribeWebhook(string unsubscribeContact)
        {
            try
            {
                //whether plugin is configured
                var sendInBlueSettings = _settingService.LoadSetting<SendInBlueSettings>();
                if (string.IsNullOrEmpty(sendInBlueSettings.ApiKey))
                    throw new NopException($"Plugin not configured");

                //parse string to JSON object
                var unsubscriber = JsonConvert.DeserializeAnonymousType(unsubscribeContact,
                    new { tag = (int?)0, email = string.Empty, date_event = string.Empty });

                //we pass the store identifier in the X-Mailin-Tag at sending emails, now get it here
                var storeId = unsubscriber?.tag;
                if (!storeId.HasValue)
                    return;

                //get subscription by email and store identifier
                var email = unsubscriber?.email;
                var subscription = _newsLetterSubscriptionService.GetNewsLetterSubscriptionByEmailAndStoreId(email, storeId.Value);
                if (subscription == null)
                    return;

                //update subscription
                subscription.Active = false;
                _newsLetterSubscriptionService.UpdateNewsLetterSubscription(subscription);
                _logger.Information($"SendInBlue unsubscription: email {email}, store #{storeId}, date {unsubscriber?.date_event}");
            }
            catch (Exception exception)
            {
                //log full error
                _logger.Error($"SendInBlue error: {exception.Message}.", exception, _workContext.CurrentCustomer);
            }
        }

        /// <summary>
        /// Create webhook to get notification about unsubscribed contacts
        /// </summary>
        /// <returns>Webhook id</returns>
        public int GetUnsubscribeWebHookId()
        {
            try
            {
                //create API client
                var client = CreateApiClient(config => new WebhooksApi(config));

                //check whether webhook already exist
                var sendInBlueSettings = _settingService.LoadSetting<SendInBlueSettings>();
                if (sendInBlueSettings.UnsubscribeWebhookId != 0)
                {
                    client.GetWebhook(sendInBlueSettings.UnsubscribeWebhookId);
                    return sendInBlueSettings.UnsubscribeWebhookId;
                }

                //or create new one
                var urlHelper = _urlHelperFactory.GetUrlHelper(_actionContextAccessor.ActionContext);
                var notificationUrl = urlHelper.RouteUrl(SendInBlueDefaults.UnsubscribeContactRoute, null, _webHelper.CurrentRequestProtocol);
                var webhook = new CreateWebhook(notificationUrl, "Unsubscribe event webhook",
                    new List<CreateWebhook.EventsEnum> { CreateWebhook.EventsEnum.Unsubscribed }, CreateWebhook.TypeEnum.Transactional);
                var result = client.CreateWebhook(webhook);

                return (int)result.Id;
            }
            catch (Exception exception)
            {
                //log full error
                _logger.Error($"SendInBlue error: {exception.Message}.", exception, _workContext.CurrentCustomer);
                return 0;
            }
        }

        /// <summary>
        /// Update contact after completing order
        /// </summary>
        /// <param name="order">Order</param>
        public void UpdateContactAfterCompletingOrder(Order order)
        {
            try
            {
                //create API client
                var client = CreateApiClient(config => new ContactsApi(config));

                //update contact
                var attributes = new Dictionary<string, string>
                {
                    [SendInBlueDefaults.IdServiceAttribute] = order.Id.ToString(),
                    [SendInBlueDefaults.OrderIdServiceAttribute] = order.Id.ToString(),
                    [SendInBlueDefaults.OrderDateServiceAttribute] = order.PaidDateUtc.ToString(),
                    [SendInBlueDefaults.OrderTotalServiceAttribute] = order.OrderTotal.ToString()
                };
                var updateContact = new UpdateContact { Attributes = attributes };
                client.UpdateContact(order.Customer.Email, updateContact);
            }
            catch (Exception exception)
            {
                //log full error
                _logger.Error($"SendInBlue error: {exception.Message}.", exception, _workContext.CurrentCustomer);
            }
        }

        #endregion

        #region Common

        /// <summary>
        /// Get account information
        /// </summary>
        /// <returns>Account info; whether marketing automation is enabled, errors if exist</returns>
        public (string Info, bool MarketingAutomationEnabled, /*bool TransactionSMSAllowed,*/  string Errors) GetAccountInfo()
        {
            try
            {
                //create API client
                var client = CreateApiClient(config => new AccountApi(config));

                //get account
                var account = client.GetAccount();

                //prepare info
                var info = string.Format("Name: {1}{0}Second name: {2}{0}Email: {3}{0}Email credits: {4}{0}SMS credits: {5}{0}",
                    Environment.NewLine,
                    WebUtility.HtmlEncode(account.FirstName),
                    WebUtility.HtmlEncode(account.LastName),
                    WebUtility.HtmlEncode(account.Email),
                    account.Plan.Where(plan => plan.Type != GetAccountPlan.TypeEnum.Sms).Sum(plan => plan.Credits),
                    account.Plan.Where(plan => plan.Type == GetAccountPlan.TypeEnum.Sms).Sum(plan => plan.Credits));

                return (info, account.MarketingAutomation?.Enabled ?? false, null);
            }
            catch (Exception exception)
            {
                //log full error
                _logger.Error($"SendInBlue error: {exception.Message}.", exception, _workContext.CurrentCustomer);
                return (null, false, exception.Message);
            }
        }

        /// <summary>
        /// Set partner value
        /// </summary>
        /// <returns>True if partner successfully set; otherwise false</returns>
        public bool SetPartner()
        {
            try
            {
                //create API client
                var client = CreateApiClient(config => new AccountApi(config));

                //set partner
                client.SetPartner(new SetPartner(SendInBlueDefaults.PartnerName));
            }
            catch (Exception exception)
            {
                //log full error
                _logger.Error($"SendInBlue error: {exception.Message}.", exception, _workContext.CurrentCustomer);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Get available lists to synchronize contacts
        /// </summary>
        /// <returns>List of id-name pairs of lists; errors if exist</returns>
        public (IList<(string Id, string Name)> Lists, string Errors) GetLists()
        {
            var availableLists = new List<(string Id, string Name)>();

            try
            {
                //create API client
                var client = CreateApiClient(config => new ContactsApi(config));

                //get available lists
                var lists = client.GetLists(SendInBlueDefaults.DefaultSynchronizationListsLimit);

                //prepare id-name pairs
                var template = new { lists = new[] { new { id = string.Empty, name = string.Empty } } };
                var listObjects = JsonConvert.DeserializeAnonymousType(lists.ToJson(), template);
                if (listObjects?.lists != null)
                {
                    foreach (var list in listObjects.lists)
                    {
                        if (list != null)
                            availableLists.Add((list.id, list.name));
                    }
                }
            }
            catch (Exception exception)
            {
                //log full error
                _logger.Error($"SendInBlue error: {exception.Message}.", exception, _workContext.CurrentCustomer);
                return (availableLists, exception.Message);
            }

            return (availableLists, null);
        }

        /// <summary>
        /// Get available senders of transactional emails
        /// </summary>
        /// <returns>List of id-name pairs of senders; errors if exist</returns>
        public (IList<(string Id, string Name)> Lists, string Errors) GetSenders()
        {
            var availableSenders = new List<(string Id, string Name)>();

            try
            {
                //create API client
                var client = CreateApiClient(config => new SendersApi(config));

                //get available senderes
                var senders = client.GetSenders();

                //prepare id-name pairs
                foreach (var sender in senders.Senders)
                {
                    availableSenders.Add((sender.Id.ToString(), $"{sender.Name} ({sender.Email})"));
                }
            }
            catch (Exception exception)
            {
                //log full error
                _logger.Error($"SendInBlue error: {exception.Message}.", exception, _workContext.CurrentCustomer);
                return (availableSenders, exception.Message);
            }

            return (availableSenders, null);
        }

        /// <summary>
        /// Chekc and create missing attributes in account
        /// </summary>
        /// <returns>Errors if exist</returns>
        public string PrepareAttributes()
        {
            try
            {
                //create API client
                var client = CreateApiClient(config => new AttributesApi(config));

                var attributes = client.GetAttributes();
                var attributeNames = attributes.Attributes.Select(s => s.Name).ToList();
                
                //prepare attributes to create
                var initialAttributes = new List<(CategoryEnum category, string Name, string Value, CreateAttribute.TypeEnum? Type)>
                {
                    (CategoryEnum.Normal, SendInBlueDefaults.UsernameServiceAttribute, null, CreateAttribute.TypeEnum.Text),
                    (CategoryEnum.Normal, SendInBlueDefaults.PhoneServiceAttribute, null, CreateAttribute.TypeEnum.Text),
                    (CategoryEnum.Normal, SendInBlueDefaults.CountryServiceAttribute, null, CreateAttribute.TypeEnum.Text),
                    (CategoryEnum.Normal, SendInBlueDefaults.StoreIdServiceAttribute, null, CreateAttribute.TypeEnum.Text),
                    (CategoryEnum.Transactional, SendInBlueDefaults.OrderIdServiceAttribute, null, CreateAttribute.TypeEnum.Id),
                    (CategoryEnum.Transactional, SendInBlueDefaults.OrderDateServiceAttribute, null, CreateAttribute.TypeEnum.Text),
                    (CategoryEnum.Transactional, SendInBlueDefaults.OrderTotalServiceAttribute, null, CreateAttribute.TypeEnum.Text),
                    (CategoryEnum.Calculated, SendInBlueDefaults.OrderTotalSumServiceAttribute, $"SUM[{SendInBlueDefaults.OrderTotalServiceAttribute}]", null),
                    (CategoryEnum.Calculated, SendInBlueDefaults.OrderTotalMonthSumServiceAttribute, $"SUM[{SendInBlueDefaults.OrderTotalServiceAttribute},{SendInBlueDefaults.OrderDateServiceAttribute},>,NOW(-30)]", null),
                    (CategoryEnum.Calculated, SendInBlueDefaults.OrderCountServiceAttribute, $"COUNT[{SendInBlueDefaults.OrderIdServiceAttribute}]", null),
                    (CategoryEnum.Global, SendInBlueDefaults.AllOrderTotalSumServiceAttribute, $"SUM[{SendInBlueDefaults.OrderTotalSumServiceAttribute}]", null),
                    (CategoryEnum.Global, SendInBlueDefaults.AllOrderTotalMonthSumServiceAttribute, $"SUM[{SendInBlueDefaults.OrderTotalMonthSumServiceAttribute}]", null),
                    (CategoryEnum.Global, SendInBlueDefaults.AllOrderCountServiceAttribute, $"SUM[{SendInBlueDefaults.OrderCountServiceAttribute}]", null)
                };

                //create attributes that are not already on account
                var newAttributes = new List<(CategoryEnum category, string Name, string Value, CreateAttribute.TypeEnum? Type)>();
                foreach (var attribute in initialAttributes)
                {
                    if (!attributeNames.Contains(attribute.Name))
                        newAttributes.Add(attribute);
                }

                return CreateAttibutes(newAttributes);
            }
            catch (Exception exception)
            {
                //log full error
                _logger.Error($"SendInBlue error: {exception.Message}.", exception, _workContext.CurrentCustomer);
                return exception.Message;
            }
        }

        /// <summary>
        /// Move message template tokens to transactional attributes
        /// </summary>
        /// <param name="tokens">List of available message templates tokens</param>
        /// <returns>Errors if exist</returns>
        public string PrepareTransactionalAttributes(IList<string> tokens)
        {
            try
            {
                //create API client
                var client = CreateApiClient(config => new AttributesApi(config));

                //get already existing transactional attributes
                var attributes = client.GetAttributes();
                var transactionalAttributes = attributes.Attributes
                    .Where(attribute => attribute.Category == CategoryEnum.Transactional).ToList();

                //bring tokens to attributes format
                tokens = tokens.Select(token => token.Replace("%", "").Replace(".", "_").Replace("(s)", "-s-").ToUpperInvariant()).ToList();

                //get attributes that are not already on account
                tokens = tokens.Except(transactionalAttributes.Select(attribute => attribute.Name)).ToList();
                if (!tokens.Any())
                    return string.Empty;

                //prepare attributes to create
                var newAttributes = new List<(CategoryEnum category, string Name, string Value, CreateAttribute.TypeEnum? Type)>();
                foreach (var token in tokens)
                {
                    newAttributes.Add((CategoryEnum.Transactional, token, null, CreateAttribute.TypeEnum.Text));
                }

                return CreateAttibutes(newAttributes);
            }
            catch (Exception exception)
            {
                //log full error
                _logger.Error($"SendInBlue error: {exception.Message}.", exception, _workContext.CurrentCustomer);
                return exception.Message;
            }
        }

        #endregion

        #region SMTP

        /// <summary>
        /// Check whether SMTP is enabled on account
        /// </summary>
        /// <returns>Result of check; errors if exist</returns>
        public (bool Enabled, string Errors) SmtpIsEnabled()
        {
            try
            {
                //create API client
                var client = CreateApiClient(config => new AccountApi(config));

                //get account
                var account = client.GetAccount();
                return (account.Relay?.Enabled ?? false, null);
            }
            catch (Exception exception)
            {
                //log full error
                _logger.Error($"SendInBlue error: {exception.Message}.", exception, _workContext.CurrentCustomer);
                return (false, exception.Message);
            }
        }

        /// <summary>
        /// Get email account identifier
        /// </summary>
        /// <param name="senderId">Sender identifier</param>
        /// <param name="smtpKey">SMTP key</param>
        /// <returns>Email account identifier; errors if exist</returns>
        public (int Id, string Errors) GetEmailAccountId(string senderId, string smtpKey)
        {
            try
            {
                //create API clients
                var sendersClient = CreateApiClient(config => new SendersApi(config));
                var accountClient = CreateApiClient(config => new AccountApi(config));

                //get all available senders
                var senders = sendersClient.GetSenders();
                if (!senders.Senders.Any())
                    return (0, "There are no senders");

                var currentSender = senders.Senders.FirstOrDefault(sender => sender.Id.ToString() == senderId);
                if (currentSender != null)
                {
                    //try to find existing email account by name and email
                    var emailAccount = _emailAccountService.GetAllEmailAccounts()
                        .FirstOrDefault(x => x.DisplayName == currentSender.Name && x.Email == currentSender.Email);
                    if (emailAccount != null)
                        return (emailAccount.Id, null);
                }

                //or create new one
                currentSender = currentSender ?? senders.Senders.FirstOrDefault();
                var relay = accountClient.GetAccount().Relay;
                var newEmailAccount = new EmailAccount
                {
                    Host = relay?.Data?.Relay,
                    Port = relay?.Data?.Port ?? 0,
                    Username = relay?.Data?.UserName,
                    Password = smtpKey,
                    EnableSsl = true,
                    Email = currentSender.Email,
                    DisplayName = currentSender.Name
                };
                _emailAccountService.InsertEmailAccount(newEmailAccount);

                return (newEmailAccount.Id, null);
            }
            catch (Exception exception)
            {
                //log full error
                _logger.Error($"SendInBlue error: {exception.Message}.", exception, _workContext.CurrentCustomer);
                return (0, exception.Message);
            }
        }

        /// <summary>
        /// Get email template identifier
        /// </summary>
        /// <param name="templateId">Current email template id</param>
        /// <param name="message">Message template</param>
        /// <param name="emailAccount">Email account</param>
        /// <returns>Email template identifier</returns>
        public int GetTemplateId(int templateId, MessageTemplate message, EmailAccount emailAccount)
        {
            try
            {
                //create API client
                var client = CreateApiClient(config => new SMTPApi(config));

                //check whether email template already exists
                if (templateId > 0)
                {
                    client.GetSmtpTemplate(templateId);
                    return templateId;
                }

                //or create new one
                if (emailAccount == null)
                    throw new NopException("Email account not configured");

                //the original body and subject of the email template are the same as that of the message template
                var body = message.Body.Replace("%if", "\"if\"").Replace("endif%", "\"endif\"");
                body = Regex.Replace(body, "(%[^\\%]*.%)", x => $"{{{{ params.{x.ToString().Replace("%", "").Replace(".", "_").ToUpperInvariant()} }}}}");
                var subject = message.Subject.Replace("%if", "\"if\"").Replace("endif%", "\"endif\"");
                subject = Regex.Replace(subject, "(%[^\\%]*.%)", x => $"{{{{ params.{x.ToString().Replace("%", "").Replace(".", "_").ToUpperInvariant()} }}}}");

                //create email template
                var createSmtpTemplate = new CreateSmtpTemplate(sender: new CreateSmtpTemplateSender(emailAccount.DisplayName, emailAccount.Email),
                    templateName: message.Name, htmlContent: body, subject: subject, isActive: true);
                var emailTemplate = client.CreateSmtpTemplate(createSmtpTemplate);

                return (int)emailTemplate.Id;
            }
            catch (Exception exception)
            {
                //log full error
                _logger.Error($"SendInBlue error: {exception.Message}.", exception, _workContext.CurrentCustomer);
                return 0;
            }
        }

        /// <summary>
        /// Convert SendInBlue email template to queued email
        /// </summary>
        /// <param name="templateId">Email template identifier</param>
        /// <returns>Queued email</returns>
        public QueuedEmail GetQueuedEmailFromTemplate(int templateId)
        {
            try
            {
                //create API client
                var client = CreateApiClient(config => new SMTPApi(config));

                if (templateId == 0)
                    throw new NopException("Message template is empty");

                //get template
                var template = client.GetSmtpTemplate(templateId);

                //bring attributes to tokens format
                var subject = Regex.Replace(template.Subject, "({{\\s*params\\..*?\\s*}})", x => $"%{x.ToString().Replace("{", "").Replace("}", "").Replace("params.", "").Replace("_", ".").Trim()}%");
                subject = subject.Replace("\"if\"", "%if").Replace("\"endif\"", "endif%");
                var body = Regex.Replace(template.HtmlContent, "({{\\s*params\\..*?\\s*}})", x => $"%{x.ToString().Replace("{", "").Replace("}", "").Replace("params.", "").Replace("_", ".").Trim()}%");
                body = body.Replace("\"if\"", "%if").Replace("\"endif\"", "endif%");

                //map template to queued email
                return new QueuedEmail
                {
                    Subject = subject,
                    Body = body,
                    FromName = template.Sender?.Name,
                    From = template.Sender?.Email
                };
            }
            catch (Exception exception)
            {
                //log full error
                _logger.Error($"SendInBlue email sending error: {exception.Message}.", exception, _workContext.CurrentCustomer);
                return null;
            }
        }

        #endregion

        #region SMS

        /// <summary>
        /// Send SMS 
        /// </summary>
        /// <param name="to">Phone number of the receiver</param>
        /// <param name="from">Name of sender</param>
        /// <param name="text">Text</param>
        public void SendSMS(string to, string from, string text)
        {
            //whether SMS notifications enabled
            var sendInBlueSettings = _settingService.LoadSetting<SendInBlueSettings>();
            if (!sendInBlueSettings.UseSmsNotifications)
                return;

            try
            {
                //check number and text
                if (string.IsNullOrEmpty(to) || string.IsNullOrEmpty(text))
                    throw new NopException("Phone number or SMS text is empty");

                //create API client
                var client = CreateApiClient(config => new TransactionalSMSApi(config));

                //create SMS data
                var transactionalSms = new SendTransacSms(sender: from, recipient: to, content: text, type: SendTransacSms.TypeEnum.Transactional);

                //send SMS
                var sms = client.SendTransacSms(transactionalSms);
                _logger.Information($"SendInBlue SMS sent: {sms?.Reference ?? $"credits remaining {sms?.RemainingCredits?.ToString()}"}");
            }
            catch (Exception exception)
            {
                //log full error
                _logger.Error($"SendInBlue SMS sending error: {exception.Message}.", exception, _workContext.CurrentCustomer);
            }
        }

        /// <summary>
        /// Send SMS campaign
        /// </summary>
        /// <param name="listId">Contact list identifier</param>
        /// <param name="from">Name of sender</param>
        /// <param name="text">Text</param>
        public string SendSMSCampaign(int listId, string from, string text)
        {
            try
            {
                //check list and text
                if (listId == 0 || string.IsNullOrEmpty(text) || string.IsNullOrEmpty(from))
                    throw new NopException("List or SMS text or sender name is empty");

                //create API client
                var client = CreateApiClient(config => new SMSCampaignsApi(config));

                //create SMS campaign
                var campaign = client.CreateSmsCampaign(new CreateSmsCampaign(name: CommonHelper.EnsureMaximumLength(text, 20),
                    sender: from, content: text, recipients: new CreateSmsCampaignRecipients(new List<long?> { listId })));

                //send campaign
                client.SendSmsCampaignNow(campaign.Id);
            }
            catch (Exception exception)
            {
                //log full error
                _logger.Error($"SendInBlue SMS sending error: {exception.Message}.", exception, _workContext.CurrentCustomer);
                return exception.Message;
            }

            return string.Empty;
        }

        #endregion

        #endregion
    }
}