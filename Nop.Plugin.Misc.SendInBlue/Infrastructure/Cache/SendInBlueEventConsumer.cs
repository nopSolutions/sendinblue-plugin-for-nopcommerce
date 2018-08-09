using Nop.Core.Domain.Messages;
using Nop.Services.Events;
using Nop.Services.Plugins;

namespace Nop.Plugin.Misc.SendInBlue.Infrastructure.Cache
{
    public class SubscriptionEventConsumer : IConsumer<EmailUnsubscribedEvent>
    {
        private readonly IPluginFinder _pluginFinder;

        public SubscriptionEventConsumer(IPluginFinder pluginFinder)
        {
            this._pluginFinder = pluginFinder;
        }

        /// <summary>
        /// Handles the event.
        /// </summary>
        /// <param name="eventMessage">The event message.</param>
        public void HandleEvent(EmailUnsubscribedEvent eventMessage)
        {
            var pluginDescriptor = _pluginFinder.GetPluginDescriptorBySystemName("Misc.SendInBlue");

            var plugin = pluginDescriptor?.Instance() as SendInBluePlugin;

            plugin?.Unsubscribe(eventMessage.Subscription.Email); 
        }
    }
}