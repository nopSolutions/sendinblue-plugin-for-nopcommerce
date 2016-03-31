using System.Collections.Generic;
using Nop.Core.Configuration;

namespace Nop.Plugin.Misc.SendInBlue
{
    public class SendInBlueSettings : ISettings
    {
        /// <summary>
        /// Gets or sets the API key
        /// </summary>
        public string ApiKey { get; set; }

        /// <summary>
        /// Gets or sets the URL that will be called once the synchronization is finished
        /// </summary>
        public string UrlSync { get; set; }

        /// <summary>
        /// Gets or sets the unsubscribe event webhook id 
        /// </summary>
        public int UnsubscribeWebhookId { get; set; }

        /// <summary>
        /// Gets or sets the list id  for synchronization
        /// </summary>
        public int ListId { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to use auto synchronization
        /// </summary>
        public bool AutoSync { get; set; }

        /// <summary>
        /// Gets or sets the period of executing auto synchronization in minutes
        /// </summary>
        public int AutoSyncEachMinutes { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to use SendInBlue SMTP
        /// </summary>
        public bool UseSendInBlueSMTP { get; set; }

        /// <summary>
        /// Gets or sets the SendInBlue SMTP email account id
        /// </summary>
        public int SendInBlueEmailAccountId { get; set; }

        /// <summary>
        /// Gets or sets the id of email sender
        /// </summary>
        public string SMTPSenderId { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to use SMS notifications
        /// </summary>
        public bool UseSMS { get; set; }

        /// <summary>
        /// Gets or sets the SMS sender name
        /// </summary>
        public string SMSFrom { get; set; }

        /// <summary>
        /// Gets or sets the phone number for SMS notifications
        /// </summary>
        public string MyPhoneNumber { get; set; }

        /// <summary>
        /// Gets or sets the ids of message templates used for SMS notifications
        /// </summary>
        public List<int> SMSMessageTemplatesIds { get; set; }
    }
}