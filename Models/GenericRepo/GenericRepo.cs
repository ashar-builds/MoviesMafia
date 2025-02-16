using Microsoft.EntityFrameworkCore;
using MoviesMafia.Configurations;
using MoviesMafia.Models.Repo;
using System.Collections.Immutable;

namespace MoviesMafia.Models.GenericRepo
{
    public class GenericRepo<T> : IGenericRepo<T> where T : class
    {
        private readonly AppDbContext _context;

        public GenericRepo(AppDbContext context)
        {
            _context = context;
        }

        public T? GetById(int id)
        {
            return _context.Set<T>().Find(id);
        }

        public IEnumerable<T> GetAll()
        {
            return _context.Set<T>().ToList();
        }

        public void Add(T entity)
        {
            _context.Set<T>().Add(entity);
        }

        public void Update(T entity)
        {
            _context.Entry(entity).State = EntityState.Modified;
        }

        public void Delete(T entity)
        {
            _context.Set<T>().Remove(entity);
        }

        public async Task<int> SaveChangesAsync()
        {
            return await _context.SaveChangesAsync();
        }
    }
}
