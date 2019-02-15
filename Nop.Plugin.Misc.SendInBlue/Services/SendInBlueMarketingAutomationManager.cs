using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.Routing;
using Nop.Core;
using Nop.Core.Domain.Directory;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Tax;
using Nop.Services.Catalog;
using Nop.Services.Common;
using Nop.Services.Directory;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Services.Media;
using Nop.Services.Orders;
using Nop.Services.Seo;
using SendinBlueMarketingAutomation.Api;
using SendinBlueMarketingAutomation.Client;
using SendinBlueMarketingAutomation.Model;

namespace Nop.Plugin.Misc.SendInBlue.Services
{
    /// <summary>
    /// Represents SendInBlue marketing automation manager
    /// </summary>
    public class SendInBlueMarketingAutomationManager
    {
        #region Fields

        private readonly CurrencySettings _currencySettings;
        private readonly IActionContextAccessor _actionContextAccessor;
        private readonly ICurrencyService _currencyService;
        private readonly IGenericAttributeService _genericAttributeService;
        private readonly ILocalizationService _localizationService;
        private readonly ILogger _logger;
        private readonly IOrderTotalCalculationService _orderTotalCalculationService;
        private readonly IPictureService _pictureService;
        private readonly IPriceCalculationService _priceCalculationService;
        private readonly IProductAttributeParser _productAttributeParser;
        private readonly IStoreContext _storeContext;
        private readonly IUrlHelperFactory _urlHelperFactory;
        private readonly IUrlRecordService _urlRecordService;
        private readonly IWebHelper _webHelper;
        private readonly IWorkContext _workContext;
        private readonly SendInBlueSettings _sendInBlueSettings;

        #endregion

        #region Ctor

        public SendInBlueMarketingAutomationManager(CurrencySettings currencySettings,
            IActionContextAccessor actionContextAccessor,
            ICurrencyService currencyService,
            IGenericAttributeService genericAttributeService,
            ILocalizationService localizationService,
            ILogger logger,
            IOrderTotalCalculationService orderTotalCalculationService,
            IPictureService pictureService,
            IPriceCalculationService priceCalculationService,
            IProductAttributeParser productAttributeParser,
            IStoreContext storeContext,
            IUrlHelperFactory urlHelperFactory,
            IUrlRecordService urlRecordService,
            IWebHelper webHelper,
            IWorkContext workContext,
            SendInBlueSettings sendInBlueSettings)
        {
            this._currencySettings = currencySettings;
            this._actionContextAccessor = actionContextAccessor;
            this._currencyService = currencyService;
            this._genericAttributeService = genericAttributeService;
            this._localizationService = localizationService;
            this._logger = logger;
            this._orderTotalCalculationService = orderTotalCalculationService;
            this._pictureService = pictureService;
            this._priceCalculationService = priceCalculationService;
            this._productAttributeParser = productAttributeParser;
            this._storeContext = storeContext;
            this._urlHelperFactory = urlHelperFactory;
            this._urlRecordService = urlRecordService;
            this._webHelper = webHelper;
            this._workContext = workContext;
            this._sendInBlueSettings = sendInBlueSettings;
        }

        #endregion

        #region Utilities

        /// <summary>
        /// Prepare marketing automation API client
        /// </summary>
        /// <returns>Marketing automation API client</returns>
        private MarketingAutomationApi CreateMarketingAutomationClient()
        {
            //validate tracker identifier
            if (string.IsNullOrEmpty(_sendInBlueSettings.MarketingAutomationKey))
                throw new NopException($"Marketing automation not configured");

            var apiConfiguration = new Configuration()
            {
                MaKey = new Dictionary<string, string> { [SendInBlueDefaults.MarketingAutomationKeyHeader] = _sendInBlueSettings.MarketingAutomationKey },
                UserAgent = SendInBlueDefaults.UserAgent
            };

            return new MarketingAutomationApi(apiConfiguration);
        }

        #endregion

        #region Methods

        /// <summary>
        /// Handle shopping cart changed event
        /// </summary>
        /// <param name="cartItem">Shopping cart item</param>
        public void HandleShoppingCartChangedEvent(ShoppingCartItem cartItem)
        {
            //whether marketing automation is enabled
            if (!_sendInBlueSettings.UseMarketingAutomation)
                return;

            try
            {
                //create API client
                var client = CreateMarketingAutomationClient();

                //first, try to identify current customer
                client.Identify(new Identify(cartItem.Customer.Email));

                //get shopping cart GUID
                var shoppingCartGuid = _genericAttributeService.GetAttribute<Guid?>(cartItem.Customer, SendInBlueDefaults.ShoppingCartGuidAttribute);

                //create track event object
                var trackEvent = new TrackEvent(cartItem.Customer.Email, string.Empty);

                //get current customer's shopping cart
                var cart = cartItem.Customer.ShoppingCartItems
                    .Where(item => item.ShoppingCartType == ShoppingCartType.ShoppingCart)
                    .LimitPerStore(_storeContext.CurrentStore.Id).ToList();

                if (cart.Any())
                {
                    //get URL helper
                    var urlHelper = _urlHelperFactory.GetUrlHelper(_actionContextAccessor.ActionContext);

                    //get shopping cart amounts
                    _orderTotalCalculationService.GetShoppingCartSubTotal(cart, _workContext.TaxDisplayType == TaxDisplayType.IncludingTax,
                        out var cartDiscount, out _, out var cartSubtotal, out _);
                    var cartTax = _orderTotalCalculationService.GetTaxTotal(cart, false);
                    var cartShipping = _orderTotalCalculationService.GetShoppingCartShippingTotal(cart);
                    var cartTotal = _orderTotalCalculationService.GetShoppingCartTotal(cart, false, false);

                    //get products data by shopping cart items
                    var productsData = cart.Where(item => item.Product != null).Select(item =>
                    {
                        //try to get product attribute combination
                        var combination = _productAttributeParser.FindProductAttributeCombination(item.Product, item.AttributesXml);

                        //get default product picture
                        var picture = _pictureService.GetProductPicture(item.Product, item.AttributesXml);

                        //get product SEO slug name
                        var seName = _urlRecordService.GetSeName(item.Product);

                        //create product data
                        return new
                        {
                            id = item.Product.Id,
                            name = _localizationService.GetLocalized(item.Product, x => x.Name),
                            variant_id = combination?.Id ?? item.Product.Id,
                            variant_id_name = combination?.Sku ?? _localizationService.GetLocalized(item.Product, x => x.Name),
                            url = urlHelper.RouteUrl("Product", new { SeName = seName }, _webHelper.CurrentRequestProtocol),
                            image = _pictureService.GetPictureUrl(picture),
                            quantity = item.Quantity,
                            price = _priceCalculationService.GetSubTotal(item),
                        };
                    }).ToArray();

                    //prepare cart data
                    var cartData = new
                    {
                        subtotal = cartSubtotal,
                        shipping = cartShipping,
                        total_before_tax = cartSubtotal + cartShipping,
                        tax = cartTax,
                        discount = cartDiscount,
                        total = cartTotal,
                        url = urlHelper.RouteUrl("ShoppingCart", null, _webHelper.CurrentRequestProtocol),
                        currency = _currencyService.GetCurrencyById(_currencySettings.PrimaryStoreCurrencyId)?.CurrencyCode,
                        //gift_wrapping = string.Empty, //currently we can't get this value
                        products = productsData
                    };

                    //if there is a single item in the cart, so the cart is just created
                    if (cart.Count == 1)
                    {
                        shoppingCartGuid = Guid.NewGuid();
                    }
                    else
                    {
                        //otherwise cart is updated
                        shoppingCartGuid = shoppingCartGuid ?? Guid.NewGuid();
                    }
                    trackEvent.EventName = SendInBlueDefaults.CartUpdatedEventName;
                    trackEvent.EventData = new { id = $"cart:{shoppingCartGuid}", data = cartData };
                }
                else
                {
                    //there are no items in the cart, so the cart is deleted
                    shoppingCartGuid = shoppingCartGuid ?? Guid.NewGuid();
                    trackEvent.EventName = SendInBlueDefaults.CartDeletedEventName;
                    trackEvent.EventData = new { id = $"cart:{shoppingCartGuid}" };
                }

                //track event
                client.TrackEvent(trackEvent);

                //update GUID for the current customer's shopping cart
                _genericAttributeService.SaveAttribute(cartItem.Customer, SendInBlueDefaults.ShoppingCartGuidAttribute, shoppingCartGuid);
            }
            catch (Exception exception)
            {
                //log full error
                _logger.Error($"SendInBlue Marketing Automation error: {exception.Message}.", exception, cartItem.Customer);
            }
        }

        /// <summary>
        /// Handle order completed event
        /// </summary>
        /// <param name="order">Order</param>
        public void HandleOrderCompletedEvent(Order order)
        {
            //whether marketing automation is enabled
            if (!_sendInBlueSettings.UseMarketingAutomation)
                return;

            try
            {
                //create API client
                var client = CreateMarketingAutomationClient();

                //first, try to identify current customer
                client.Identify(new Identify(order.Customer.Email));

                //get URL helper
                var urlHelper = _urlHelperFactory.GetUrlHelper(_actionContextAccessor.ActionContext);

                //get products data by order items
                var productsData = order.OrderItems.Where(item => item.Product != null).Select(item =>
                {
                    //try to get product attribute combination
                    var combination = _productAttributeParser.FindProductAttributeCombination(item.Product, item.AttributesXml);

                    //get default product picture
                    var picture = _pictureService.GetProductPicture(item.Product, item.AttributesXml);

                    //get product SEO slug name
                    var seName = _urlRecordService.GetSeName(item.Product);

                    //create product data
                    return new
                    {
                        id = item.Product.Id,
                        name = _localizationService.GetLocalized(item.Product, x => x.Name),
                        variant_id = combination?.Id ?? item.Product.Id,
                        variant_id_name = combination?.Sku ?? _localizationService.GetLocalized(item.Product, x => x.Name),
                        url = urlHelper.RouteUrl("Product", new { SeName = seName }, _webHelper.CurrentRequestProtocol),
                        image = _pictureService.GetPictureUrl(picture),
                        quantity = item.Quantity,
                        price = item.PriceInclTax,
                    };
                }).ToArray();

                //prepare cart data
                var cartData = new
                {
                    subtotal = order.OrderSubtotalInclTax,
                    shipping = order.OrderShippingInclTax,
                    total_before_tax = order.OrderSubtotalInclTax + order.OrderShippingInclTax,
                    tax = order.OrderTax,
                    discount = order.OrderDiscount,
                    total = order.OrderTotal,
                    url = urlHelper.RouteUrl("OrderDetails", new { orderId = order.Id }, _webHelper.CurrentRequestProtocol),
                    currency = order.CustomerCurrencyCode,
                    //gift_wrapping = string.Empty, //currently we can't get this value
                    products = productsData
                };

                //get shopping cart GUID
                var shoppingCartGuid = _genericAttributeService.GetAttribute<Guid?>(order,
                    SendInBlueDefaults.ShoppingCartGuidAttribute) ?? Guid.NewGuid();

                //create track event object
                var trackEvent = new TrackEvent(order.Customer.Email, SendInBlueDefaults.OrderCompletedEventName,
                    eventData: new { id = $"cart:{shoppingCartGuid}", data = cartData });

                //track event
                client.TrackEvent(trackEvent);

                //update GUID for the current customer's shopping cart
                _genericAttributeService.SaveAttribute<Guid?>(order, SendInBlueDefaults.ShoppingCartGuidAttribute, null);
            }
            catch (Exception exception)
            {
                //log full error
                _logger.Error($"SendInBlue Marketing Automation error: {exception.Message}.", exception, order.Customer);
            }
        }

        /// <summary>
        /// Handle order placed event
        /// </summary>
        /// <param name="order">Order</param>
        public void HandleOrderPlacedEvent(Order order)
        {
            //whether marketing automation is enabled
            if (!_sendInBlueSettings.UseMarketingAutomation)
                return;

            //copy shopping cart GUID to order
            var shoppingCartGuid = _genericAttributeService.GetAttribute<Guid?>(order.Customer, SendInBlueDefaults.ShoppingCartGuidAttribute);
            _genericAttributeService.SaveAttribute(order, SendInBlueDefaults.ShoppingCartGuidAttribute, shoppingCartGuid);
        }

        #endregion
    }
}