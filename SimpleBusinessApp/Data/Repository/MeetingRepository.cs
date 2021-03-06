﻿using System.Threading.Tasks;
using SimpleBusinessApp.Data.Repositories;
using SimpleBusinessApp.DataAccess;
using SimpleBusinessApp.Model;
using System.Data.Entity;
using System.Collections.Generic;
using System.Linq;

namespace SimpleBusinessApp.Data.Repository
{
    public class MeetingRepository : GenericRepository<Meeting, ClientOrganizerDbContext>, IMeetingRepository
    {
        public MeetingRepository(ClientOrganizerDbContext context) : base (context)
        {

        }

        public async override Task<Meeting> GetByIdAsync(int id)
        {
            return await Context.Meetings
                .Include(m => m.Clients)
                .SingleAsync(m => m.Id == id);
        }

        public async Task<List<Client>> GetAllClientsAsync ()
        {
            return await Context.Set<Client>()
                .ToListAsync();
        }

        public async Task ReloadClientAsync(int clientId)
        {
            var dbEntityEntry = Context.ChangeTracker.Entries<Client>()
                .SingleOrDefault(db => db.Entity.Id == clientId);
            if (dbEntityEntry != null)
            {
                await dbEntityEntry.ReloadAsync();
            }
        }
    }
}
