using Autofac;
using Nop.Core.Configuration;
using Nop.Core.Infrastructure;
using Nop.Core.Infrastructure.DependencyManagement;
using Nop.Plugin.Misc.SendInBlue.Services;
using Nop.Services.Messages;

namespace Nop.Plugin.Misc.SendInBlue.Infrastructure
{
    /// <summary>
    /// Represents a SendInBlue dependency registrar
    /// </summary>
    public class DependencyRegistrar : IDependencyRegistrar
    {
        /// <summary>
        /// Register services and interfaces
        /// </summary>
        /// <param name="builder">Container builder</param>
        /// <param name="typeFinder">Type finder</param>
        /// <param name="config">Config</param>
        public void Register(ContainerBuilder builder, ITypeFinder typeFinder, NopConfig config)
        {
            //register custom services
            builder.RegisterType<SendInBlueManager>().AsSelf().InstancePerLifetimeScope();
            builder.RegisterType<SendInBlueMarketingAutomationManager>().AsSelf().InstancePerLifetimeScope();

            //override services
            builder.RegisterType<SendInBlueMessageService>().As<IWorkflowMessageService>().InstancePerLifetimeScope();
            builder.RegisterType<SendInBlueEmailSender>().As<IEmailSender>().InstancePerLifetimeScope();
        }

        /// <summary>
        /// Order of this dependency registrar implementation
        /// </summary>
        public int Order => 2;
    }
}