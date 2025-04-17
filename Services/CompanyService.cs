using GoogleLogin.Migrations;
using GoogleLogin.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using ShopifySharp;
using System.Text;
using System.Text.Json;

namespace GoogleLogin.Services
{
    public class CompanyService
    {
        private readonly IServiceScopeFactory       _serviceScopeFactory;
        private readonly ILogger<ShopifyService>    _logger;
        private readonly IConfiguration             _configuration;
		public CompanyService(
            IServiceScopeFactory serviceScopeFactory, 
            IConfiguration configuration, 
            ILogger<ShopifyService> logger)
        {
            _serviceScopeFactory    = serviceScopeFactory;
            _logger                 = logger;
            _configuration          = configuration;
        }

        public long addCompany(string name, string site)
        {
            using (var scope = _serviceScopeFactory.CreateScope())
            {
                var _dbContext = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();

                try
                {
                    var _one = _dbContext
                        .TbCompanies
                        .Where(e => e.name == name)
                        .FirstOrDefault();

                    if (_one != null)
                    {
                        return -1;
                    }

                    var newCompany = new TbCompany
                    {
                        name = name,
                        site = site
                    };

                    _dbContext.Add(newCompany);
                    _dbContext.SaveChanges();

                    return newCompany.id;
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.StackTrace);
                }
            }

            return -1;
        }

        public List<TbCompany> getCompanies(string email)
        {
            using (var scope = _serviceScopeFactory.CreateScope())
            {
                var _dbContext = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();

                var companyList = (from company in _dbContext.TbCompanies
                                   join member in _dbContext.TbMembers
                                   on company.id equals member.companyIdx
                                   where member.email == email
                                   select company).ToList();

                return companyList;
            }
        }

        public TbCompany? getCompany(string name)
        {
            using (var scope = _serviceScopeFactory.CreateScope())
            {
                var _dbContext = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();

                try
                {
                    var _one = _dbContext
                        .TbCompanies
                        .Where(e => e.name == name)
                        .FirstOrDefault();

                    return _one;
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.StackTrace);
                }
            }

            return null;
        }

        public TbCompany? getCompanyByIdx(long companyIdx)
        {
            using (var scope = _serviceScopeFactory.CreateScope())
            {
                var _dbContext = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();

                try
                {
                    var _one = _dbContext
                        .TbCompanies
                        .Where(e => e.id == companyIdx)
                        .FirstOrDefault();

                    return _one;
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.StackTrace);
                }
            }

            return null;
        }

        public int deleteCompany(TbCompany company)
        {
            using (var scope = _serviceScopeFactory.CreateScope())
            {
                var _dbContext = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();

                _dbContext.TbCompanies.Remove(company);
                _dbContext.SaveChanges();

                return 1;
            }
        }
    }
}
