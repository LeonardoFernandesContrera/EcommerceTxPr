using EcommerceTxPr.Domain.Shared;

namespace EcommerceTxPr.Aplication.Services
{
    public interface IBaseService<T> where T : class
    {
        Task<Result<IEnumerable<T>, Error>> GetAllAsync();
        Task<Result<T, Error>> GetByIdAsync(int Id);
        Task<Result<string, Error>> CreateAsync(T obj);
        Task<Result<string, Error>> UpdateAsync(T obj);
        Task<Result<string, Error>> DeleteByIdAsync(int Id);
    }
}
