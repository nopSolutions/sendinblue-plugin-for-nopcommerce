using Nop.Core.Domain.Messages;
using Nop.Core.Domain.Orders;
using Nop.Core.Events;
using Nop.Services.Events;

namespace Nop.Plugin.Misc.SendInBlue.Services
{
    /// <summary>
    /// Represents a SendInBlue event consumer
    /// </summary>
    public class EventConsumer :
        IConsumer<EmailUnsubscribedEvent>,
        IConsumer<EmailSubscribedEvent>,
        IConsumer<EntityInsertedEvent<ShoppingCartItem>>,
        IConsumer<EntityUpdatedEvent<ShoppingCartItem>>,
        IConsumer<EntityDeletedEvent<ShoppingCartItem>>,
        IConsumer<OrderPaidEvent>,
        IConsumer<OrderPlacedEvent>
    {
        #region Fields

        private readonly SendInBlueManager _sendInBlueEmailManager;
        private readonly SendInBlueMarketingAutomationManager _sendInBlueMarketingAutomationManager;

        #endregion

        #region Ctor

        public EventConsumer(SendInBlueManager sendInBlueEmailManager,
            SendInBlueMarketingAutomationManager sendInBlueMarketingAutomationManager)
        {
            _sendInBlueEmailManager = sendInBlueEmailManager;
            _sendInBlueMarketingAutomationManager = sendInBlueMarketingAutomationManager;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Handle the email unsubscribed event.
        /// </summary>
        /// <param name="eventMessage">The event message.</param>
        public void HandleEvent(EmailUnsubscribedEvent eventMessage)
        {
            //unsubscribe contact
            _sendInBlueEmailManager.Unsubscribe(eventMessage.Subscription);
        }

        /// <summary>
        /// Handle the email subscribed event.
        /// </summary>
        /// <param name="eventMessage">The event message.</param>
        public void HandleEvent(EmailSubscribedEvent eventMessage)
        {
            //subscribe contact
            _sendInBlueEmailManager.Subscribe(eventMessage.Subscription);
        }

        /// <summary>
        /// Handle the add shopping cart item event
        /// </summary>
        /// <param name="eventMessage">The event message.</param>
        public void HandleEvent(EntityInsertedEvent<ShoppingCartItem> eventMessage)
        {
            //handle event
            _sendInBlueMarketingAutomationManager.HandleShoppingCartChangedEvent(eventMessage.Entity);
        }

        /// <summary>
        /// Handle the update shopping cart item event
        /// </summary>
        /// <param name="eventMessage">The event message.</param>
        public void HandleEvent(EntityUpdatedEvent<ShoppingCartItem> eventMessage)
        {
            //handle event
            _sendInBlueMarketingAutomationManager.HandleShoppingCartChangedEvent(eventMessage.Entity);
        }

        /// <summary>
        /// Handle the delete shopping cart item event
        /// </summary>
        /// <param name="eventMessage">The event message.</param>
        public void HandleEvent(EntityDeletedEvent<ShoppingCartItem> eventMessage)
        {
            //handle event
            _sendInBlueMarketingAutomationManager.HandleShoppingCartChangedEvent(eventMessage.Entity);
        }

        /// <summary>
        /// Handle the order paid event
        /// </summary>
        /// <param name="eventMessage">The event message.</param>
        public void HandleEvent(OrderPaidEvent eventMessage)
        {
            //handle event
            _sendInBlueMarketingAutomationManager.HandleOrderCompletedEvent(eventMessage.Order);
            _sendInBlueEmailManager.UpdateContactAfterCompletingOrder(eventMessage.Order);
        }

        /// <summary>
        /// Handle the order placed event
        /// </summary>
        /// <param name="eventMessage">The event message.</param>
        public void HandleEvent(OrderPlacedEvent eventMessage)
        {
            //handle event
            _sendInBlueMarketingAutomationManager.HandleOrderPlacedEvent(eventMessage.Order);
        }

        #endregion
    }
}