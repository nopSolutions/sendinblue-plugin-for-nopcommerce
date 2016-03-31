using Nop.Core.Plugins;
using Nop.Services.Tasks;

namespace Nop.Plugin.Misc.SendInBlue.Services
{
    public class SendInBlueSynchronizationTask : ITask
    {
        private readonly IPluginFinder _pluginFinder;

        public SendInBlueSynchronizationTask(IPluginFinder pluginFinder)
        {
            this._pluginFinder = pluginFinder;
        }

        /// <summary>
        /// Execute task
        /// </summary>
        public void Execute()
        {
            var pluginDescriptor = _pluginFinder.GetPluginDescriptorBySystemName("Misc.SendInBlue");
            if (pluginDescriptor == null)
                return;

            var plugin = pluginDescriptor.Instance() as SendInBluePlugin;
            if (plugin == null)
                return;

            plugin.Synchronize();                
        }
    }
}