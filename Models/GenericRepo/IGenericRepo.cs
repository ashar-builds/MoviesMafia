namespace MoviesMafia.Models.GenericRepo
{
    public interface IGenericRepo<T> where T : class
    {
        T? GetById(int id);
        IEnumerable<T> GetAll();
        void Add(T entity);
        void Update(T entity);
        void Delete(T entity);

        Task<int> SaveChangesAsync();
    }

}
