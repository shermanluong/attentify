using GoogleLogin.Migrations;
using GoogleLogin.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ShopifySharp;
using System.Text;
using System.Text.Json;

namespace GoogleLogin.Services
{
    public class MemberService
    {
        private readonly IServiceScopeFactory       _serviceScopeFactory;
        private readonly ILogger<ShopifyService>    _logger;
        private readonly IConfiguration             _configuration;
		public MemberService(
            IServiceScopeFactory serviceScopeFactory, 
            IConfiguration configuration, 
            ILogger<ShopifyService> logger)
        {
            _serviceScopeFactory    = serviceScopeFactory;
            _logger                 = logger;
            _configuration          = configuration;
        }

        public long addMember(string email, long companyIdx, int role)
        {
            using (var scope = _serviceScopeFactory.CreateScope())
            {
                var _dbContext = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();

                try
                {
                    var _one = _dbContext
                        .TbMembers
                        .Where(item => item.email == email && item.companyIdx == companyIdx )
                        .FirstOrDefault();

                    if (_one != null)
                    {
                        return -1;
                    }

                    var newMember = new TbMember
                    {
                        email = email,
                        companyIdx = companyIdx,
                        role = role
                    };

                    _dbContext.Add(newMember);
                    _dbContext.SaveChanges();

                    return newMember.id;
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.StackTrace);
                }
            }

            return -1;
        }

        public List<TbMember> getMembers(long companyIdx)
        {
            using (var scope = _serviceScopeFactory.CreateScope())
            {
                var _dbContext = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();

                var memberList = _dbContext
                        .TbMembers
                        .Where(item => item.companyIdx == companyIdx)
                        .ToList();

                return memberList;
            }
        }

        public TbMember? getMember(string email, long companyIdx)
        {
            using (var scope = _serviceScopeFactory.CreateScope())
            {
                var _dbContext = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();

                try
                {
                    var _one = _dbContext
                        .TbMembers
                        .Where(item => item.email == email && item.companyIdx == companyIdx)
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

        public TbMember? getMemberByIdx(long memberIdx)
        {
            using (var scope = _serviceScopeFactory.CreateScope())
            {
                var _dbContext = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();

                try
                {
                    var _one = _dbContext
                        .TbMembers
                        .Where(item => item.id == memberIdx)
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

        public int deleteMemeberByCompanyId(long companyIdx)
        {
            using (var scope = _serviceScopeFactory.CreateScope())
            {
                var _dbContext = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();

                try
                {
                    var members = _dbContext
                        .TbMembers
                        .Where(item => item.companyIdx == companyIdx)
                        .ToList();

                    if ( members.Any() )
                    {
                        _dbContext.TbMembers.RemoveRange(members);
                        _dbContext.SaveChanges();
                        return 1;
                    } else
                    {
                        return 0;
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.StackTrace);
                }
            }

            return -1;
        }

        public int deleteMember(TbMember member)
        {
            using (var scope = _serviceScopeFactory.CreateScope())
            {
                var _dbContext = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();

                _dbContext.TbMembers.Remove(member);
                _dbContext.SaveChanges();

                return 1;
            }
        }
    }
}
