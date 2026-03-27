using MailMonitor.Domain.Abstractions;
using MediatR;

namespace MailMonitor.Application.Abstractions.Messaging
{
    public interface IBaseCommand : IRequest<Result>
    {
    }

    public interface IBaseCommand<TResponse> : IRequest<Result<TResponse>>
    {
    }

    public interface IBaseQuery<TResponse> : IRequest<Result<TResponse>>
    {
    }
}
