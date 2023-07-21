using System.Threading.Tasks;
using FluentResults;

namespace OoLunar.HyperSharp
{
    public interface IResponder
    {
        string[] Implements { get; init; }

        Task<Result> RespondAsync(object context);
    }

    public interface IResponder<TInput, TOutput> : IResponder
    {
        Task<Result<TOutput>> RespondAsync(TInput context);
        async Task<Result> IResponder.RespondAsync(object context) => (Result)(ResultBase)await RespondAsync((TInput)context);
    }
}
