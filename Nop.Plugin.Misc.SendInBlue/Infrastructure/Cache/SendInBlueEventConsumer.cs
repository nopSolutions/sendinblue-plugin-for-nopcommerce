using Nop.Core.Domain.Messages;
using Nop.Services.Events;
using Nop.Services.Plugins;
using Nop.Core.Events;
using Nop.Core.Domain.Orders;

namespace Nop.Plugin.Misc.SendInBlue.Infrastructure.Cache
{
    public class SubscriptionEventConsumer : 
        IConsumer<EmailUnsubscribedEvent>, 
        IConsumer<EmailSubscribedEvent>, 
        IConsumer<EntityInsertedEvent<ShoppingCartItem>>,
        IConsumer<EntityUpdatedEvent<ShoppingCartItem>>,
        IConsumer<EntityDeletedEvent<ShoppingCartItem>>,
        IConsumer<OrderPaidEvent>
    {
        private readonly IPluginFinder _pluginFinder;

        public SubscriptionEventConsumer(IPluginFinder pluginFinder)
        {
            this._pluginFinder = pluginFinder;
        }

        /// <summary>
        /// Handle the email unsubscribed event.
        /// </summary>
        /// <param name="eventMessage">The event message.</param>
        public void HandleEvent(EmailUnsubscribedEvent eventMessage)
        {
            var pluginDescriptor = _pluginFinder.GetPluginDescriptorBySystemName("Misc.SendInBlue");

            var plugin = pluginDescriptor?.Instance() as SendInBluePlugin;

            plugin?.Unsubscribe(eventMessage.Subscription.Email); 
        }

        /// <summary>
        /// Handle the email subscribed event.
        /// </summary>
        /// <param name="eventMessage">The event message.</param>
        public void HandleEvent(EmailSubscribedEvent eventMessage)
        {
            var pluginDescriptor = _pluginFinder.GetPluginDescriptorBySystemName("Misc.SendInBlue");

            var plugin = pluginDescriptor?.Instance() as SendInBluePlugin;

            plugin?.Subscribe(eventMessage.Subscription.Email);
        }

        /// <summary>
        /// Handle the add shopping cart item event
        /// </summary>
        /// <param name="eventMessage">The event message.</param>
        public void HandleEvent(EntityInsertedEvent<ShoppingCartItem> eventMessage)
        {
            var pluginDescriptor = _pluginFinder.GetPluginDescriptorBySystemName("Misc.SendInBlue");

            var plugin = pluginDescriptor?.Instance() as SendInBluePlugin;

            plugin?.CartCreated(eventMessage.Entity);
        }

        /// <summary>
        /// Handle the update shopping cart item event
        /// </summary>
        /// <param name="eventMessage">The event message.</param>
        public void HandleEvent(EntityUpdatedEvent<ShoppingCartItem> eventMessage)
        {
            var pluginDescriptor = _pluginFinder.GetPluginDescriptorBySystemName("Misc.SendInBlue");

            var plugin = pluginDescriptor?.Instance() as SendInBluePlugin;

            plugin?.CartUpdated(eventMessage.Entity);
        }

        /// <summary>
        /// Handle the delete shopping cart item event
        /// </summary>
        /// <param name="eventMessage">The event message.</param>
        public void HandleEvent(EntityDeletedEvent<ShoppingCartItem> eventMessage)
        {
            var pluginDescriptor = _pluginFinder.GetPluginDescriptorBySystemName("Misc.SendInBlue");

            var plugin = pluginDescriptor?.Instance() as SendInBluePlugin;
            
            plugin?.CartDeleted(eventMessage.Entity);
        }

        /// <summary>
        /// Handle the order paid event
        /// </summary>
        /// <param name="eventMessage">The event message.</param>
        public void HandleEvent(OrderPaidEvent eventMessage)
        {
            var pluginDescriptor = _pluginFinder.GetPluginDescriptorBySystemName("Misc.SendInBlue");

            var plugin = pluginDescriptor?.Instance() as SendInBluePlugin;

            plugin?.OrderCompleted(eventMessage.Order);
        }
    }
}