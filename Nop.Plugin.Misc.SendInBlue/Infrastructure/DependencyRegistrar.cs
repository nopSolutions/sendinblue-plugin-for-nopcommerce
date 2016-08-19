using Autofac;
using Nop.Core.Configuration;
using Nop.Core.Infrastructure;
using Nop.Core.Infrastructure.DependencyManagement;
using Nop.Services.Messages;
using Nop.Plugin.Misc.SendInBlue.Services;

namespace Nop.Plugin.Misc.SendInBlue.Infrastructure 
{
    public class DependencyRegistrar : IDependencyRegistrar
    {
        /// <summary>
        /// Registers the specified builder.
        /// </summary>
        /// <param name="builder">The builder.</param>
        /// <param name="typeFinder">The type finder.</param>
        /// <param name="config"></param>
        public void Register(ContainerBuilder builder, ITypeFinder typeFinder, NopConfig config)
        {
            builder.RegisterType<SendInBlueEmailManager>().AsSelf().InstancePerLifetimeScope();
            builder.RegisterType<SendInBlueMessageService>().As<IWorkflowMessageService>().InstancePerLifetimeScope();
            builder.RegisterType<SendInBlueEmailSender>().As<IEmailSender>().InstancePerLifetimeScope();
        }

        /// <summary>
        /// Gets the order.
        /// </summary>
        public int Order
        {
            get { return 1; }
        }
    }
}