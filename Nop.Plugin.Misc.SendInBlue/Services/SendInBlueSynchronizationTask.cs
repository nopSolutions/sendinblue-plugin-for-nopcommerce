using Nop.Core.Plugins;
using Nop.Services.Tasks;

namespace Nop.Plugin.Misc.SendInBlue.Services
{
    public class SendInBlueSynchronizationTask : IScheduleTask
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

            var plugin = pluginDescriptor?.Instance() as SendInBluePlugin;

            plugin?.Synchronize();                
        }
    }
}