using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AgricHub.DAL.Entities.Models;
using AgricHub.DAL.Entities;

namespace AgricHub.DAL
{
    public static class DataSeeder
    {
       


        public static async Task SeedRoles(RoleManager<IdentityRole> roleManager)
        {
            var roles = new[] { "Admin", "Consultant", "Customer"};

            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new IdentityRole(role));
                }
            }
        }


    }
}
