using EcommerceTxPr.Domain.Shared;

namespace EcommerceTxPr.Application.Repositories
{
    public interface IBaseRepository<T> where T : class
    {
        Task<Result<IEnumerable<T>, Error>> GetAllAsync();
        Task<Result<T, Error>> GetByIdAsync(Guid id);
        Task<Result<string, Error>> CreateAsync(T obj);
        Task<Result<string, Error>> UpdateAsync(T obj);
        Task<Result<string, Error>> DeleteByIdAsync(Guid id);
    }
}
