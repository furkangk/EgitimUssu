using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Shared.Application;

public interface ICommandAuthorizer<in TCommand>
{
    Task<Result> Authorize(TCommand command, CancellationToken cancellationToken);
}
