using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Entities;
using Microsoft.EntityFrameworkCore;

namespace Repository
{
    public class PromotionRepository : IPromotionRepository
    {
        private readonly StoreContext _context;
        public PromotionRepository(StoreContext context) => _context = context;

        public async Task<IEnumerable<Promotion>> GetAllAsync() =>
            await _context.Promotions.Take(100).ToListAsync();

        public async Task<Promotion> CreateAsync(Promotion promotion)
        {
            await _context.Promotions.AddAsync(promotion);
            await _context.SaveChangesAsync();
            return promotion;
        }
    }
}
