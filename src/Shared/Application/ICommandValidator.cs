using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Shared.Application;

public interface ICommandValidator<in TCommand>
{
    Task<Result> Validate(TCommand command, CancellationToken cancellationToken);
}
