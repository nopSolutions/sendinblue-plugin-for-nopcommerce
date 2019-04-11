using Nop.Services.Plugins;
using Nop.Services.Tasks;

namespace Nop.Plugin.Misc.SendinBlue.Services
{
    /// <summary>
    /// Represents a schedule task to synchronize contacts
    /// </summary>
    public class SynchronizationTask : IScheduleTask
    {
        #region Fields

        private readonly IPluginFinder _pluginFinder;
        private readonly SendinBlueManager _sendInBlueEmailManager;

        #endregion

        #region Ctor

        public SynchronizationTask(IPluginFinder pluginFinder,
            SendinBlueManager sendInBlueEmailManager)
        {
            _pluginFinder = pluginFinder;
            _sendInBlueEmailManager = sendInBlueEmailManager;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Execute task
        /// </summary>
        public void Execute()
        {
            //check whether a plugin is installed
            var pluginDescriptor = _pluginFinder.GetPluginDescriptorBySystemName(SendinBlueDefaults.SystemName);
            if (!pluginDescriptor.Installed || !(pluginDescriptor?.Instance() is SendinBluePlugin plugin))
                return;

            //synchronize
            _sendInBlueEmailManager.Synchronize();
        }

        #endregion
    }
}