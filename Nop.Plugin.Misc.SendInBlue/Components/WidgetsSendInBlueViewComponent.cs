using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Core.Domain.Customers;
using Nop.Services.Logging;
using Nop.Web.Framework.Components;
using System;

namespace Nop.Plugin.Misc.SendinBlue.Components
{
    [ViewComponent(Name = "WidgetsSendinBlue")]
    public class WidgetsSendinBlueViewComponent: NopViewComponent
    {
        #region Fields

        private readonly SendinBlueSettings _sendInBlueSettings;
        private readonly ILogger _logger;
        private readonly IStoreContext _storeContext;
        private readonly IWorkContext _workContext;

        #endregion

        #region Ctor

        public WidgetsSendinBlueViewComponent(
            SendinBlueSettings sendInBlueSettings,
            ILogger logger,
            IStoreContext storeContext,
            IWorkContext workContext)
        {
            _sendInBlueSettings = sendInBlueSettings;
            _logger = logger;
            _storeContext = storeContext;
            _workContext = workContext;
        }

        #endregion

        #region Utilities

        private string FixIllegalJavaScriptChars(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            text = text.Replace("'", "\\'");
            return text;
        }

        private string GetEcommerceScript()
        {
            var trackingScript = _sendInBlueSettings.TrackingScript + "\n";
            trackingScript = trackingScript.Replace("{TRACKING_ID}", _sendInBlueSettings.MarketingAutomationKey);

            //whether to include customer identifier
            var customerEmail = string.Empty;
            if (!_workContext.CurrentCustomer.IsGuest())
                customerEmail = _workContext.CurrentCustomer.Email;
            trackingScript = trackingScript.Replace("{CUSTOMER_EMAIL}", FixIllegalJavaScriptChars(customerEmail));

            return trackingScript;
        }

        #endregion

        #region Methods

        public IViewComponentResult Invoke(string widgetZone, object additionalData)
        {
            string script = "";
            var routeData = Url.ActionContext.RouteData;

            try
            {
                var controller = routeData.Values["controller"];
                var action = routeData.Values["action"];

                if (controller == null || action == null)
                    return Content("");

                if (_sendInBlueSettings.UseMarketingAutomation)
                {
                    script += GetEcommerceScript();
                }
            }
            catch (Exception ex)
            {
                _logger.InsertLog(Core.Domain.Logging.LogLevel.Error, "Error creating scripts for SendinBlue tracking", ex.ToString());
            }
            return View("~/Plugins/Misc.SendinBlue/Views/PublicInfo.cshtml", script);
        }

        #endregion


    }
}
