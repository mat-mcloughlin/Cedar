namespace Cedar.Handlers
{
    using System;
    using System.Security.Claims;
    using System.Threading.Tasks;
    using Cedar.Commands;
    using Cedar.Domain;
    using Cedar.Domain.Persistence;

    public class DomainCommandMessageHandlerModule : MessageHandlerModule
    {
        public DomainCommandMessageHandlerModule(Func<IRepository> repository)
        {
            ForMessage<CommandMessage<CreateAggregate>>()
                .LogExceptions()
                .DenyAnonymous()
                .RequiresClaim(claim => claim.Type == ClaimTypes.Email)
                .ValidateWith(Command1Validator.Instance)
                .Finally((message, ct) => repository().Save(new Aggregate1(message.Command), message.CommandId));

            ForMessage<CommandMessage<CancelAggregate>>()
                .LogExceptions()
                .DenyAnonymous()
                .RequiresClaim(claim => claim.Type == ClaimTypes.Email)
                .Handle(next => next)
                .Finally((message, ct) => /* etc */ Task.FromResult(0));

            ForMessage<CommandMessage<OtherOperationOnAggregate>>()
                .PerformanceCounter()
                .LogAuthorizeAndValidate(Command1Validator.Instance)
                .RequiresClaim(claim => claim.Type == ClaimTypes.Email)
                .Finally((message, ct) => /* etc */ Task.FromResult(0));
        }
    }

    public class CreateAggregate
    {
        public static string NewItemId { get; set; }
    }

    public class Command1Validator
    {
        public static Command1Validator Instance = new Command1Validator();
    }

    public class CancelAggregate { }

    public class OtherOperationOnAggregate { }

    public class Aggregate1 : AggregateBase
    {
        protected Aggregate1(string id)
            : base(id)
        { }

        public Aggregate1(CreateAggregate command)
            : this(CreateAggregate.NewItemId)
        { }
    }
}