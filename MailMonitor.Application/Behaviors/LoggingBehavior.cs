using MailMonitor.Domain.Abstractions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace MailMonitor.Application.Behaviors
{
    internal sealed class LoggingBehavior<TRequest, TResponse>
        : IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull, IBaseRequest
        where TResponse : Result
    {
        private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

        public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
        {
            _logger = logger;
        }

        public async Task<TResponse> Handle(
            TRequest request,
            RequestHandlerDelegate<TResponse> next,
            CancellationToken cancellationToken)
        {
            var requestName = request.GetType().Name;

            try
            {
                _logger.LogInformation("Executing request {RequestName}", requestName);

                var result = await next();

                if (result.IsSuccess)
                {
                    _logger.LogInformation("Request {RequestName} processed successfully", requestName);
                }
                else
                {
                    _logger.LogError("Request {RequestName} processed with error {ErrorCode}: {ErrorName}", requestName, result.Error.Code, result.Error.Name);
                }

                return result;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Request {RequestName} processing failed", requestName);
                throw;
            }
        }
    }
}
