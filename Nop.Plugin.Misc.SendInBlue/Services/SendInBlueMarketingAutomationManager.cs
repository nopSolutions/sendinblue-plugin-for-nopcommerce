using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Nop.Core;
using Nop.Core.Domain.Orders;
using Nop.Services.Common;
using Nop.Services.Configuration;
using Nop.Services.Logging;
using Nop.Services.Media;
using Nop.Services.Orders;
using SendinBlueMarketingAutomation.Api;
using SendinBlueMarketingAutomation.Client;
using SendinBlueMarketingAutomation.Model;

namespace Nop.Plugin.Misc.SendInBlue.Services
{
    public class SendInBlueMarketingAutomationManager
    {
        #region Fields

        private readonly IGenericAttributeService _genericAttributeService;
        private readonly ILogger _logger;
        private readonly IPictureService _pictureService;
        private readonly ISettingService _settingService;
        private readonly IStoreContext _storeContext;        

        #endregion

        #region Ctor

        public SendInBlueMarketingAutomationManager(
            IGenericAttributeService genericAttributeService,
            ILogger logger,
            IPictureService pictureService,
            ISettingService settingService,
            IStoreContext storeContext)
        {
            this._genericAttributeService = genericAttributeService;
            this._logger = logger;
            this._pictureService = pictureService;
            this._settingService = settingService;
            this._storeContext = storeContext;
        }

        #endregion

        #region Private properties

        /// <summary>
        /// Gets a value indicating whether API key is specified
        /// </summary>
        private bool IsConfigured => !string.IsNullOrEmpty(_settingService.LoadSetting<SendInBlueSettings>().MAKey) && _settingService.LoadSetting<SendInBlueSettings>().UseMA;

        /// <summary>
        /// Gets configuration for SendInBlue API
        /// </summary>
        private Configuration Config => new Configuration()
        {
            MaKey = new Dictionary<string, string> { { "ma-key", _settingService.LoadSetting<SendInBlueSettings>().MAKey } }
        };

        /// <summary>
        /// Gets a collection of functions to interact with the API endpoints of Marketing Automation 
        /// </summary>
        private MarketingAutomationApi MarketingAutomationApi => new MarketingAutomationApi(Config);

        #endregion

        /// <summary>
        /// Gets shopping cart items
        /// </summary>
        /// <param name="cartItem"></param>
        /// <returns>Shopping cart</returns>
        private List<ShoppingCartItem> GetShoppingCart(ShoppingCartItem cartItem)
        {
            return cartItem.Customer.ShoppingCartItems
                    .Where(sci => sci.ShoppingCartType == ShoppingCartType.ShoppingCart)
                    .LimitPerStore(_storeContext.CurrentStore.Id)
                    .ToList();
        }

        /// <summary>
        /// Gets a status shopping cart
        /// </summary>
        /// <param name="cartItem"></param>
        /// 2 - cart is empty
        /// <returns>StatusCart</returns>
        private StatusCart GetStatusCart(ShoppingCartItem cartItem)
        {
            var cart = GetShoppingCart(cartItem);
            if (!cart.Any())
                return StatusCart.Deleted;

            if (cart.Count() == 1)
                return StatusCart.Created;

            if (cart.Count() > 1)
                return StatusCart.Updated;

            return 0;
        }


        /// <summary>
        /// Generated data for track event
        /// </summary>
        /// <param name="cartItem">Shopping cart item</param>
        /// <returns>Object</returns>
        private object GenerateTrackData(ShoppingCartItem cartItem)
        {
            var cart = GetShoppingCart(cartItem);
            var products = new List<object>();

            foreach (var item in cart)
            {
                var product = new Dictionary<string, object>()
                {
                    { "id", item.Product.Sku },
                    { "name", item.Product.Name },
                    { "variant_id", null },
                    { "variant_id_name", item.Product.Sku },
                    { "url", "" },
                    { "image", _pictureService.GetPictureUrl(_pictureService.GetProductPicture(item.Product, null)) },
                    { "quantity", item.Quantity },
                    { "price", item.Product.Price },
                };

                products.Add(product);
            }

            var data = new Dictionary<string, object>()
                {
                    { "subtotal", ""},
                    { "total_before_tax" , "" },
                    { "tax", "" },
                    { "discount", "" },
                    { "total", "" },
                    { "url", "" },
                    { "currency", "" },
                    { "gift_wrapping", "" },
                    { "products", products }
                };

            var result  = new Dictionary<string, object>()
            {
                { "id", "cart:" + _genericAttributeService.GetAttribute<string>(cartItem.Customer, "ShoppingCartGuid")},
                { "data",  data}
            };

            return result;
        }


        public void CartCreated(ShoppingCartItem cartItem)
        {
            if (!IsConfigured)
                _logger.Error("SendInBlue Marketing Automation error: Plugin not configured");

            _genericAttributeService.SaveAttribute(cartItem.Customer, "ShoppingCartGuid", Guid.NewGuid().ToString());

            if (GetStatusCart(cartItem) == StatusCart.Created)
            {

                var obj = GenerateTrackData(cartItem);

                Identify(cartItem.Customer.Email, null);
                TrackEvent(cartItem.Customer.Email, "cart_created", obj);
            }
        }

        public void CartUpdated(ShoppingCartItem cartItem)
        {
            if (!IsConfigured)
                _logger.Error("SendInBlue Marketing Automation error: Plugin not configured");

            if (GetStatusCart(cartItem) == StatusCart.Updated)
            {

                var obj = GenerateTrackData(cartItem);

                Identify(cartItem.Customer.Email, null);
                TrackEvent(cartItem.Customer.Email, "cart_updated", obj);
            }
        }

        public void CartDeleted(ShoppingCartItem cartItem)
        {
            if (!IsConfigured)
                _logger.Error("SendInBlue Marketing Automation error: Plugin not configured");

            if (GetStatusCart(cartItem) == StatusCart.Deleted)
            {

                var obj = GenerateTrackData(cartItem);

                Identify(cartItem.Customer.Email, null);
                TrackEvent(cartItem.Customer.Email, "cart_deleted", obj);
            }
        }

        public void OrderCompleted(Order order)
        {
            if (!IsConfigured)
                _logger.Error("SendInBlue Marketing Automation error: Plugin not configured");

            _genericAttributeService.SaveAttribute(order.Customer, "ShoppingCartGuid", Guid.NewGuid().ToString());

            var products = new List<object>();

            foreach (var item in order.OrderItems)
            {
                var product = new Dictionary<string, object>()
                {
                    { "id", item.Product.Sku },
                    { "name", item.Product.Name },
                    { "variant_id", null },
                    { "variant_id_name", item.Product.Sku },
                    { "url", "" },
                    { "image", _pictureService.GetPictureUrl(_pictureService.GetProductPicture(item.Product, null)) },
                    { "quantity", item.Quantity },
                    { "price", item.Product.Price },
                };

                products.Add(product);
            }

            var data = new Dictionary<string, object>()
                {
                    { "subtotal", order.OrderSubtotalInclTax.ToString()},
                    { "total_before_tax" , order.OrderSubtotalExclTax.ToString() },
                    { "tax", order.OrderTax.ToString() },
                    { "discount", order.OrderDiscount.ToString() },
                    { "total", order.OrderTotal.ToString() },
                    { "url", "" },
                    { "currency", order.CustomerCurrencyCode },
                    { "gift_wrapping", "" },
                    { "products", products }
                };

            var result = new Dictionary<string, object>()
            {
                { "id", "cart:" + _genericAttributeService.GetAttribute<string>(order.Customer, "ShoppingCartGuid")},
                { "data",  data}
            };

            Identify(order.Customer.Email, null);
                TrackEvent(order.Customer.Email, "order_completed", result);
        }

        #region Methods (wrappers)

        private void Identify(string email, object attributes)
        {
            try
            {
                var identify = new Identify(email, attributes);
                MarketingAutomationApi.Identify(identify);
            }
            catch (ApiException e)
            {
                _logger.Error($"SendInBlue Marketing Automation identify error: {e.Message}");
            }
        }


        public void TrackEvent(string email, string eventName, object attributes)
        {
            try
            {
                var trackEvent = new TrackEvent(email, eventName, attributes);
                MarketingAutomationApi.TrackEvent(trackEvent);
            }
            catch (ApiException e)
            {
                _logger.Error($"SendInBlue Marketing Automation trackEvent error: {e.Message}");
            }
        }


        private void TrackLink(string email, string link, object properties)
        {
            try
            {
                var trackLink = new TrackLink(email, link, properties);
                MarketingAutomationApi.TrackLink(trackLink);
            }
            catch (ApiException e)
            {
                _logger.Error($"SendInBlue Marketing Automation trackLink error: {e.Message}");
            }
        }

        private void TrackPage(string email, string page, object properties)
        {
            try
            {
                var trackPage = new TrackPage(email, page, properties);
                MarketingAutomationApi.TrackPage(trackPage);
            }
            catch (ApiException e)
            {
                _logger.Error($"SendInBlue Marketing Automation trackPage error: {e.Message}");
            }
        }

        #endregion
    }
}
