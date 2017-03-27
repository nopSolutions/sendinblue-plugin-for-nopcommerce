using Nop.Core.Domain.Messages;
using Nop.Core.Plugins;
using Nop.Services.Events;

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
            if (pluginDescriptor == null)
                return;

            var plugin = pluginDescriptor.Instance() as SendInBluePlugin;
            if (plugin == null)
                return;

            plugin.Unsubscribe(eventMessage.Subscription.Email); 
        }
    }
}