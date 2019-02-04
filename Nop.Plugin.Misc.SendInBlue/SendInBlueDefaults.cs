using Nop.Core;

namespace Nop.Plugin.Misc.SendInBlue
{
    /// <summary>
    /// Represents constants of the SendInBlue plugin
    /// </summary>
    public static class SendInBlueDefaults
    {
        /// <summary>
        /// Gets a plugin system name
        /// </summary>
        public static string SystemName => "Misc.SendInBlue";

        /// <summary>
        /// Gets a plugin partner name
        /// </summary>
        public static string PartnerName => "NOPCOMMERCE";

        /// <summary>
        /// Gets a user agent used for requesting SendInBlue services
        /// </summary>
        public static string UserAgent => $"nopCommerce-{NopVersion.CurrentVersion}";

        /// <summary>
        /// Gets a URL to edit message template on SendInBlue account
        /// </summary>
        public static string EditMessageTemplateUrl = "https://my.sendinblue.com/camp/template/{0}/message-setup?utm_source=nopcommerce_plugin&utm_medium=plugin&utm_campaign=module_link";

        /// <summary>
        /// Gets a name of the route to the import contacts callback
        /// </summary>
        public static string ImportContactsRoute => "Plugin.Misc.SendInBlue.ImportContacts";

        /// <summary>
        /// Gets a name of the route to the unsubscribe contact callback
        /// </summary>
        public static string UnsubscribeContactRoute => "Plugin.Misc.SendInBlue.Unsubscribe";

        /// <summary>
        /// Gets a name of the synchronization schedule task
        /// </summary>
        public static string SynchronizationTaskName => "Synchronization (SendInBlue plugin)";

        /// <summary>
        /// Gets a type of the synchronization schedule task
        /// </summary>
        public static string SynchronizationTask => "Nop.Plugin.Misc.SendInBlue.Services.SynchronizationTask";

        /// <summary>
        /// Gets a default synchronization period in hours
        /// </summary>
        public static int DefaultSynchronizationPeriod => 12;

        /// <summary>
        /// Gets a header of the API authentication key
        /// </summary>
        public static string ApiKeyHeader => "api-key";

        /// <summary>
        /// Gets a name of attribute to store an email
        /// </summary>
        public static string EmailServiceAttribute => "EMAIL";

        /// <summary>
        /// Gets a name of attribute to store a first name
        /// </summary>
        public static string FirstNameServiceAttribute => "NAME";

        /// <summary>
        /// Gets a name of attribute to store a last name
        /// </summary>
        public static string LastNameServiceAttribute => "SURNAME";

        /// <summary>
        /// Gets a name of attribute to store a username
        /// </summary>
        public static string UsernameServiceAttribute => "USERNAME";

        /// <summary>
        /// Gets a name of attribute to store a phone
        /// </summary>
        public static string PhoneServiceAttribute => "PHONE";

        /// <summary>
        /// Gets a name of attribute to store a country
        /// </summary>
        public static string CountryServiceAttribute => "COUNTRY";

        /// <summary>
        /// Gets a name of attribute to store a store identifier
        /// </summary>
        public static string StoreIdServiceAttribute => "STORE_ID";

        /// <summary>
        /// Gets a name of attribute to store an identifier
        /// </summary>
        public static string IdServiceAttribute => "ID";

        /// <summary>
        /// Gets a name of attribute to store an order identifier
        /// </summary>
        public static string OrderIdServiceAttribute => "ORDER_ID";

        /// <summary>
        /// Gets a name of attribute to store an order date
        /// </summary>
        public static string OrderDateServiceAttribute => "ORDER_DATE";

        /// <summary>
        /// Gets a name of attribute to store an order total
        /// </summary>
        public static string OrderTotalServiceAttribute => "ORDER_PRICE";

        /// <summary>
        /// Gets a name of attribute to store an order total sum
        /// </summary>
        public static string OrderTotalSumServiceAttribute => "NOPCOMMERCE_CA_USER";

        /// <summary>
        /// Gets a name of attribute to store an order total sum of month
        /// </summary>
        public static string OrderTotalMonthSumServiceAttribute => "NOPCOMMERCE_LAST_30_DAYS_CA";

        /// <summary>
        /// Gets a name of attribute to store an order count
        /// </summary>
        public static string OrderCountServiceAttribute => "NOPCOMMERCE_ORDER_TOTAL";

        /// <summary>
        /// Gets a name of attribute to store all orders total sum
        /// </summary>
        public static string AllOrderTotalSumServiceAttribute => "NOPCOMMERCE_CA_TOTAL";

        /// <summary>
        /// Gets a name of attribute to store all orders total sum of month
        /// </summary>
        public static string AllOrderTotalMonthSumServiceAttribute => "NOPCOMMERCE_CA_LAST_30DAYS";

        /// <summary>
        /// Gets a name of attribute to store all orders count
        /// </summary>
        public static string AllOrderCountServiceAttribute => "NOPCOMMERCE_ORDERS_COUNT";

        /// <summary>
        /// Gets a key of the attribute to store shopping cart identifier
        /// </summary>
        public static string ShoppingCartGuidAttribute => "ShoppingCartGuid";

        /// <summary>
        /// Gets a header of the marketing automation authentication key
        /// </summary>
        public static string MarketingAutomationKeyHeader => "ma-key";

        /// <summary>
        /// Gets a key of the attribute to store a value indicating whether non-standard template used
        /// </summary>
        public static string SendInBlueTemplateAttribute => "SendInBlueTemplate";

        /// <summary>
        /// Gets a key of the attribute to store template identifier
        /// </summary>
        public static string TemplateIdAttribute => "TemplateId";

        /// <summary>
        /// Gets a key of the attribute to store a value indicating whether use SMS notification
        /// </summary>
        public static string UseSmsAttribute => "UseSmsNotifications";

        /// <summary>
        /// Gets a key of the attribute to store SMS text
        /// </summary>
        public static string SmsTextAttribute => "SMSText";

        /// <summary>
        /// Gets a key of the attribute to store phone type
        /// </summary>
        public static string PhoneTypeAttribute => "PhoneTypeId";

        /// <summary>
        /// Gets a name of custom email header 
        /// </summary>
        public static string EmailCustomHeader => "X-Mailin-Tag";

        /// <summary>
        /// Gets a name of the cart created event
        /// </summary>
        public static string CartCreatedEventName => "cart_created";

        /// <summary>
        /// Gets a name of the cart updated event
        /// </summary>
        public static string CartUpdatedEventName => "cart_updated";

        /// <summary>
        /// Gets a name of the cart deleted event
        /// </summary>
        public static string CartDeletedEventName => "cart_deleted";

        /// <summary>
        /// Gets a name of the order completed event
        /// </summary>
        public static string OrderCompletedEventName => "order_completed";
    }
}