using MoviesMafia.Models.GenericRepo;

namespace MoviesMafia.Models.Repo
{
    public interface IRecordsRepo : IGenericRepo<Records>
    {
        Task<List<Records>> GetRecordsByUserIdAsync(string userId);
    }
}
